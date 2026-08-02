#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;

namespace FTG_Framework.Data;

internal enum MovePersistenceFaultPoint
{
    AfterSerialize,
    BeforeInitialIdentityRead,
    BeforeStageFlush,
    AfterStage,
    AfterStagedValidation,
    BeforeCoordinatedCommit,
    BeforeContainmentRecheck,
    BeforeReplace,
    DuringErrorCleanup,
    DuringPostCommitCleanup,
}

internal enum MoveSaveStatus { Succeeded, Conflict, ValidationFailed, Failed }

internal sealed record MoveSaveResult(
    MoveSaveStatus Status,
    MoveContentIdentity? ExpectedIdentity = null,
    MoveContentIdentity? CurrentIdentity = null,
    IReadOnlyList<MoveValidationError>? Errors = null,
    string? Diagnostic = null);

internal sealed class MoveDatasetPersistence
{
    private readonly string _root;
    private readonly DataStore _store;
    private readonly Action<MovePersistenceFaultPoint>? _fault;
    private readonly Action<string>? _diagnostic;
    private readonly Func<string, bool> _isReparsePoint;
    private readonly Dictionary<string, string> _destinationIdentities = new(PathComparer);
    private readonly object _destinationIdentitySync = new();
    private static readonly ConcurrentDictionary<string, object> DestinationCommitLocks =
        new(PathComparer);

    internal MoveDatasetPersistence(
        string root, DataStore store,
        Action<MovePersistenceFaultPoint>? fault = null,
        Action<string>? diagnostic = null,
        Func<string, bool>? isReparsePoint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _isReparsePoint = isReparsePoint ?? IsReparsePoint;
        if (!Directory.Exists(_root))
            throw new DirectoryNotFoundException($"[Data] Move-data root does not exist: {_root}");
        RejectReparsePoints(_root, _root);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _fault = fault;
        _diagnostic = diagnostic;
    }

    internal MoveDatasetDocument Load(string documentIdentifier)
    {
        string path = Resolve(documentIdentifier);
        byte[] bytes = ReadConfinedBytes(path);
        return MoveDatasetCodec.Parse(System.Text.Encoding.UTF8.GetString(bytes));
    }

    internal MoveSaveResult Save(string documentIdentifier, MoveAuthoringCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        string destination = Resolve(documentIdentifier);
        RejectReparsePoints(_root, destination);
        MoveValidationResult validation = MoveDatasetCodec.Validate(candidate);
        if (!validation.Success)
            return new MoveSaveResult(MoveSaveStatus.ValidationFailed, candidate.SourceIdentity, Errors: validation.Errors);

        MoveValidationError[] missingProfiles = candidate.Moves
            .Select((move, index) => (move, index))
            .Where(item => _store.GetKnockbackProfile(item.move.KnockbackProfileId) is null)
            .Select(item => new MoveValidationError(
                $"moves[{item.index}].knockback_profile_id", item.move.KnockbackProfileId,
                "Referenced KnockbackProfile does not exist.", "Select an existing knockback profile."))
            .ToArray();
        if (missingProfiles.Length > 0)
        {
            return new MoveSaveResult(MoveSaveStatus.ValidationFailed, candidate.SourceIdentity, Errors: missingProfiles);
        }

        MoveContentIdentity currentIdentity;
        try
        {
            _fault?.Invoke(MovePersistenceFaultPoint.BeforeInitialIdentityRead);
            currentIdentity = ReadConfinedIdentity(destination);
        }
        catch (Exception ex)
        {
            return new MoveSaveResult(MoveSaveStatus.Failed, candidate.SourceIdentity,
                Diagnostic: $"[Data] Destination identity read failed: {ex.Message}");
        }
        if (currentIdentity != candidate.SourceIdentity)
            return new MoveSaveResult(MoveSaveStatus.Conflict, candidate.SourceIdentity, currentIdentity);
        if (cancellationToken.IsCancellationRequested)
            return new MoveSaveResult(MoveSaveStatus.Failed, candidate.SourceIdentity, currentIdentity,
                Diagnostic: "[Data] Save cancelled before commit.");

        var document = candidate.ToDocument();
        byte[] bytes;
        try
        {
            bytes = MoveDatasetCodec.Serialize(document);
            _fault?.Invoke(MovePersistenceFaultPoint.AfterSerialize);
        }
        catch (MoveDatasetFormatException ex)
        {
            return new MoveSaveResult(MoveSaveStatus.ValidationFailed, candidate.SourceIdentity,
                Errors: new[] { ex.ToError() });
        }
        catch (Exception ex)
        {
            return new MoveSaveResult(MoveSaveStatus.Failed, candidate.SourceIdentity, currentIdentity, Diagnostic: ex.Message);
        }

        string staged = Path.Combine(_root, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        bool committed = false;
        string? diagnostic = null;
        try
        {
            staged = PhysicsDataPersistence.StageSameDirectory(destination, bytes,
                () => _fault?.Invoke(MovePersistenceFaultPoint.BeforeStageFlush));
            _fault?.Invoke(MovePersistenceFaultPoint.AfterStage);
            byte[] stagedBytes = ReadConfinedBytes(staged);
            EnsureExpectedStagedBytes(bytes, stagedBytes);
            var stagedDocument = MoveDatasetCodec.Parse(System.Text.Encoding.UTF8.GetString(stagedBytes));
            _fault?.Invoke(MovePersistenceFaultPoint.AfterStagedValidation);
            ulong expectedDatasetVersion = _store.MoveDatasetVersion;
            _fault?.Invoke(MovePersistenceFaultPoint.BeforeCoordinatedCommit);
            bool won = _store.TryCommitMoveDataset(stagedDocument.Moves.ToArray(), expectedDatasetVersion, () =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                lock (DestinationCommitLocks.GetOrAdd(destination, static _ => new object()))
                {
                    _fault?.Invoke(MovePersistenceFaultPoint.BeforeContainmentRecheck);
                    var accessIdentity = ReadConfinedIdentity(destination);
                    if (accessIdentity != candidate.SourceIdentity)
                        return false;
                    _fault?.Invoke(MovePersistenceFaultPoint.BeforeReplace);

                    // No injectable/user code may run after these final boundary checks.
                    byte[] finalStagedBytes = ReadConfinedBytes(staged);
                    EnsureExpectedStagedBytes(bytes, finalStagedBytes);
                    var finalDestinationIdentity = ReadConfinedIdentity(destination);
                    if (finalDestinationIdentity != candidate.SourceIdentity)
                        return false;
                    PhysicsDataPersistence.ReplaceStaged(staged, destination);
                    committed = true;
                    return true;
                }
            });
            if (!won)
            {
                try
                {
                    currentIdentity = ReadConfinedIdentity(destination);
                    return new MoveSaveResult(MoveSaveStatus.Conflict, candidate.SourceIdentity, currentIdentity);
                }
                catch (Exception ex)
                {
                    return new MoveSaveResult(MoveSaveStatus.Failed, candidate.SourceIdentity,
                        Diagnostic: $"[Data] Conflict identity read failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            if (committed)
            {
                diagnostic = $"[Data] Save committed; post-commit diagnostic: {ex.Message}";
                _diagnostic?.Invoke(diagnostic);
            }
            else
                return new MoveSaveResult(MoveSaveStatus.Failed, candidate.SourceIdentity, currentIdentity, Diagnostic: ex.Message);
        }
        finally
        {
            if (committed)
            {
                try { _fault?.Invoke(MovePersistenceFaultPoint.DuringPostCommitCleanup); }
                catch (Exception ex)
                {
                    diagnostic = $"[Data] Save committed; staging cleanup failed: {ex.Message}";
                    _diagnostic?.Invoke(diagnostic);
                }
            }
            if (File.Exists(staged))
            {
                try
                {
                    if (!committed) _fault?.Invoke(MovePersistenceFaultPoint.DuringErrorCleanup);
                    PhysicsDataPersistence.CleanupOwnedStaging(staged);
                }
                catch (Exception ex)
                {
                    if (committed)
                    {
                        diagnostic = $"[Data] Save committed; staging cleanup failed: {ex.Message}";
                        _diagnostic?.Invoke(diagnostic);
                    }
                }
            }
        }
        return new MoveSaveResult(MoveSaveStatus.Succeeded, candidate.SourceIdentity,
            MoveContentIdentity.FromBytes(bytes), Diagnostic: diagnostic);
    }

    internal MoveSaveResult Restore(
        string documentIdentifier, IReadOnlyList<MoveAuthoringMove> desiredMoves)
    {
        var current = Load(documentIdentifier);
        var candidate = new MoveAuthoringCandidate(
            MoveDatasetCodec.CurrentSchemaVersion,
            new System.Collections.ObjectModel.ReadOnlyCollection<MoveAuthoringMove>(desiredMoves.ToArray()),
            current.ContentIdentity);
        return Save(documentIdentifier, candidate);
    }

    private string Resolve(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || Path.IsPathRooted(identifier) ||
            identifier.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
            throw new ArgumentException("[Data] Document identifier must be a non-empty path-free ordinal identifier.", nameof(identifier));
        string path = Path.GetFullPath(Path.Combine(_root, identifier + ".json"));
        string prefix = _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, PathComparison))
            throw new ArgumentException("[Data] Document path escapes the configured move-data root.", nameof(identifier));
        lock (_destinationIdentitySync)
        {
            if (_destinationIdentities.TryGetValue(path, out string? registered) &&
                !string.Equals(registered, identifier, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"[Data] Document identifier '{identifier}' aliases destination already owned by '{registered}'.",
                    nameof(identifier));
            _destinationIdentities[path] = identifier;
        }
        return path;
    }

    private void RejectReparsePoints(string root, string destination)
    {
        string? current = File.Exists(destination) ? destination : Path.GetDirectoryName(destination);
        while (current is not null && current.Length >= root.Length)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if (_isReparsePoint(current))
                    throw new IOException($"[Data] Reparse points are not allowed in the move-data path: {current}");
            }
            if (string.Equals(current, root, PathComparison)) break;
            current = Path.GetDirectoryName(current);
        }
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private MoveContentIdentity ReadConfinedIdentity(string destination)
    {
        return MoveContentIdentity.FromBytes(ReadConfinedBytes(destination));
    }

    private byte[] ReadConfinedBytes(string path)
    {
        RejectReparsePoints(_root, path);
        byte[] bytes = File.ReadAllBytes(path);
        RejectReparsePoints(_root, path);
        return bytes;
    }

    private static void EnsureExpectedStagedBytes(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
            throw new IOException("[Data] Staged move dataset changed after validation.");
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
