#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FTG_Framework.Core;

namespace FTG_Framework.Data;

internal enum TrainingPersistenceFaultPoint
{
    AfterSerialize,
    BeforeStageFlush,
    AfterStage,
    AfterStagedValidation,
    BeforeCoordinatedCommit,
    BeforeContainmentRecheck,
    BeforeReplace,
    DuringErrorCleanup,
    DuringPostCommitCleanup,
}

internal enum TrainingSaveStatus { Succeeded, ValidationFailed, Failed }

internal sealed record TrainingSaveResult(
    TrainingSaveStatus Status,
    string? Error = null,
    string? Sha256 = null);

/// <summary>
/// Failure-atomic save/load of training snapshots (P-SAVE). The save file is a
/// canonical UTF-8 JSON document whose embedded container is integrity-protected
/// by a lowercase-hex SHA-256 over the exact container bytes; a manual edit is
/// never implicitly valid. Save stages in the destination directory and atomically
/// replaces the prior file only after flush, byte verification, and re-validation;
/// any failure preserves the prior save byte-for-byte.
/// </summary>
internal static class TrainingStatePersistence
{
    public const int SaveSchemaVersion = 1;
    public const int MaxFileBytes = 16 * 1024 * 1024;
    public const int MaxComponentRecords = 64;
    public const int MaxComponentPayloadBytes = 4 * 1024 * 1024;
    public const int MaxDiscriminatorLength = 128;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    internal static TrainingSaveResult Save(StateSnapshot snapshot, string destinationPath,
        Action<TrainingPersistenceFaultPoint>? fault = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        byte[] containerBytes;
        try
        {
            containerBytes = StateSnapshotCodec.Encode(snapshot);
            fault?.Invoke(TrainingPersistenceFaultPoint.AfterSerialize);
        }
        catch (Exception ex)
        {
            return new TrainingSaveResult(TrainingSaveStatus.Failed,
                $"[Save] Container serialization failed: {ex.Message}");
        }

        string? limitError = ValidateSaveLimits(snapshot, containerBytes.Length);
        if (limitError is not null)
            return new TrainingSaveResult(TrainingSaveStatus.ValidationFailed, limitError);

        string digest = Sha256Hex(containerBytes);
        byte[] fileBytes;
        try
        {
            fileBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildFileJson(containerBytes, digest));
        }
        catch (Exception ex)
        {
            return new TrainingSaveResult(TrainingSaveStatus.Failed,
                $"[Save] Save document serialization failed: {ex.Message}");
        }
        // The file limit applies to the complete save document (container +
        // wrapper), matching Load's check; otherwise a save near the limit would
        // be written successfully but be permanently unloadable.
        if (fileBytes.Length > MaxFileBytes)
            return new TrainingSaveResult(TrainingSaveStatus.ValidationFailed,
                $"[Save] Save document is {fileBytes.Length} bytes, exceeding the {MaxFileBytes}-byte P-SAVE limit.");

        string staged = string.Empty;
        bool committed = false;
        string? diagnostic = null;
        try
        {
            staged = PhysicsDataPersistence.StageSameDirectory(destinationPath, fileBytes,
                () => fault?.Invoke(TrainingPersistenceFaultPoint.BeforeStageFlush));
            fault?.Invoke(TrainingPersistenceFaultPoint.AfterStage);
            byte[] stagedBytes = File.ReadAllBytes(staged);
            EnsureExpectedStagedBytes(fileBytes, stagedBytes);
            ValidateFileDocument(stagedBytes);
            fault?.Invoke(TrainingPersistenceFaultPoint.AfterStagedValidation);
            fault?.Invoke(TrainingPersistenceFaultPoint.BeforeCoordinatedCommit);
            CanonicalDestinationCoordinator.Execute(destinationPath, () =>
            {
                fault?.Invoke(TrainingPersistenceFaultPoint.BeforeContainmentRecheck);
                byte[] finalStagedBytes = File.ReadAllBytes(staged);
                EnsureExpectedStagedBytes(fileBytes, finalStagedBytes);
                fault?.Invoke(TrainingPersistenceFaultPoint.BeforeReplace);
                PhysicsDataPersistence.ReplaceStaged(staged, destinationPath);
                committed = true;
                return true;
            });
        }
        catch (Exception ex)
        {
            if (committed)
            {
                diagnostic = $"[Save] Save committed; post-commit diagnostic: {ex.Message}";
                FrameworkLog.Error?.Invoke(diagnostic);
            }
            else
            {
                return new TrainingSaveResult(TrainingSaveStatus.Failed,
                    $"[Save] {ex.Message}");
            }
        }
        finally
        {
            if (committed)
            {
                try { fault?.Invoke(TrainingPersistenceFaultPoint.DuringPostCommitCleanup); }
                catch (Exception ex)
                {
                    diagnostic = $"[Save] Save committed; staging cleanup failed: {ex.Message}";
                    FrameworkLog.Error?.Invoke(diagnostic);
                }
            }
            if (File.Exists(staged))
            {
                if (!committed)
                {
                    try { fault?.Invoke(TrainingPersistenceFaultPoint.DuringErrorCleanup); }
                    catch (Exception ex)
                    {
                        diagnostic = $"[Save] Error-cleanup fault: {ex.Message}";
                        FrameworkLog.Error?.Invoke(diagnostic);
                    }
                }
                try { PhysicsDataPersistence.CleanupOwnedStaging(staged); }
                catch (Exception ex)
                {
                    if (committed)
                    {
                        diagnostic = $"[Save] Save committed; staging cleanup failed: {ex.Message}";
                        FrameworkLog.Error?.Invoke(diagnostic);
                    }
                }
            }
        }
        CommittedDocumentRegistry.Observe(destinationPath, new DataContentIdentity(digest));
        return new TrainingSaveResult(TrainingSaveStatus.Succeeded, Sha256: digest);
    }

    internal static StateSnapshot Load(string sourcePath, out string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        long length;
        try
        {
            length = new FileInfo(sourcePath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new TrainingStateLoadException($"[Load] Cannot inspect save file: {ex.Message}");
        }
        if (length > MaxFileBytes)
            throw new TrainingStateLoadException(
                $"[Load] Save file is {length} bytes, exceeding the {MaxFileBytes}-byte P-SAVE limit.");

        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(sourcePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TrainingStateLoadException($"[Load] Cannot read save file: {ex.Message}");
        }
        StateSnapshot snapshot = DecodeFileBytes(fileBytes, out digest);
        return snapshot;
    }

    internal static string ComputeDigest(StateSnapshot snapshot)
    {
        byte[] containerBytes = StateSnapshotCodec.Encode(snapshot);
        return Sha256Hex(containerBytes);
    }

    internal static StateSnapshot DecodeFileBytes(byte[] fileBytes, out string digest)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        if (fileBytes.Length > MaxFileBytes)
            throw new TrainingStateLoadException(
                $"[Load] Save document is {fileBytes.Length} bytes, exceeding the {MaxFileBytes}-byte P-SAVE limit.");
        byte[] containerBytes = ExtractVerifiedContainer(fileBytes, out digest);
        StateSnapshot snapshot;
        try
        {
            snapshot = StateSnapshotCodec.Decode(containerBytes);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or JsonException)
        {
            throw new TrainingStateLoadException($"[Load] Save document container is invalid: {ex.Message}");
        }
        string? limitError = ValidateSaveLimits(snapshot, containerBytes.Length);
        if (limitError is not null)
            throw new TrainingStateLoadException(limitError);
        return snapshot;
    }

    internal static void ValidateFileDocument(byte[] fileBytes)
    {
        _ = ExtractVerifiedContainer(fileBytes, out _);
    }

    internal static byte[] ExtractVerifiedContainer(byte[] fileBytes, out string digest)
    {
        FileDocumentDto document;
        try
        {
            document = JsonSerializer.Deserialize<FileDocumentDto>(fileBytes, Options)
                ?? throw new TrainingStateLoadException("[Load] Save document is empty.");
        }
        catch (TrainingStateLoadException) { throw; }
        catch (JsonException ex)
        {
            throw new TrainingStateLoadException($"[Load] Save document is not valid JSON: {ex.Message}");
        }
        if (document.SaveSchemaVersion != SaveSchemaVersion)
            throw new TrainingStateLoadException(
                $"[Load] Unsupported save schema version {document.SaveSchemaVersion}; expected {SaveSchemaVersion}.");
        if (document.Container.ValueKind != JsonValueKind.Object)
            throw new TrainingStateLoadException("[Load] Save document 'container' must be an object.");
        if (!string.Equals(document.Integrity?.Algorithm, "sha256", StringComparison.OrdinalIgnoreCase))
            throw new TrainingStateLoadException("[Load] Save document integrity algorithm must be 'sha256'.");
        if (!string.Equals(document.Integrity?.InputEncoding, "utf8", StringComparison.OrdinalIgnoreCase))
            throw new TrainingStateLoadException("[Load] Save document integrity input encoding must be 'utf8'.");
        if (string.IsNullOrWhiteSpace(document.Integrity?.Digest))
            throw new TrainingStateLoadException("[Load] Save document integrity digest is missing.");

        byte[] containerBytes;
        try
        {
            containerBytes = JsonSerializer.SerializeToUtf8Bytes(document.Container, Options);
        }
        catch (JsonException ex)
        {
            throw new TrainingStateLoadException($"[Load] Save document container is malformed: {ex.Message}");
        }
        string actual = Sha256Hex(containerBytes);
        if (!string.Equals(actual, document.Integrity.Digest, StringComparison.OrdinalIgnoreCase))
            throw new TrainingStateLoadException(
                "[Load] Save document integrity check failed; the file was edited or corrupted.");
        digest = document.Integrity.Digest;
        return containerBytes;
    }

    private static string BuildFileJson(byte[] containerBytes, string digest)
    {
        JsonElement container;
        try
        {
            using var document = JsonDocument.Parse(containerBytes);
            container = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("[Save] Container is not valid canonical JSON.", ex);
        }
        var wrapper = new FileDocumentDto(SaveSchemaVersion, container, new IntegrityDto("sha256", "utf8", digest));
        return JsonSerializer.Serialize(wrapper, Options);
    }

    private static string? ValidateSaveLimits(StateSnapshot snapshot, int containerByteCount)
    {
        if (containerByteCount > MaxFileBytes)
            return $"[Save] Container is {containerByteCount} bytes, exceeding the {MaxFileBytes}-byte P-SAVE limit.";
        if (snapshot.Components.Count > MaxComponentRecords)
            return $"[Save] Component count {snapshot.Components.Count} exceeds the {MaxComponentRecords}-record P-SAVE limit.";
        foreach (SnapshotComponent component in snapshot.Components)
        {
            int length = component.Discriminator.Length;
            if (length == 0 || length > MaxDiscriminatorLength || !IsAscii(component.Discriminator))
                return $"[Save] Discriminator '{component.Discriminator}' is not {MaxDiscriminatorLength} or fewer ASCII characters.";
            int payloadBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetByteCount(component.Payload);
            if (payloadBytes > MaxComponentPayloadBytes)
                return $"[Save] Component '{component.Discriminator}' payload is {payloadBytes} bytes, exceeding the {MaxComponentPayloadBytes}-byte P-SAVE limit.";
        }
        return null;

        static bool IsAscii(string value)
        {
            foreach (char character in value)
                if (character > 127) return false;
            return true;
        }
    }

    private static string Sha256Hex(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void EnsureExpectedStagedBytes(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new IOException("[Save] Staged save document changed after validation.");
    }

    private sealed record FileDocumentDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("save_schema_version")] int SaveSchemaVersion,
        [property: System.Text.Json.Serialization.JsonPropertyName("container")] JsonElement Container,
        [property: System.Text.Json.Serialization.JsonPropertyName("integrity")] IntegrityDto? Integrity);

    private sealed record IntegrityDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("algorithm")] string Algorithm,
        [property: System.Text.Json.Serialization.JsonPropertyName("input_encoding")] string InputEncoding,
        [property: System.Text.Json.Serialization.JsonPropertyName("digest")] string Digest);
}

public sealed class TrainingStateLoadException : Exception
{
    public TrainingStateLoadException(string message) : base(message) { }
}
