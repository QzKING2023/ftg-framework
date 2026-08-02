#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace FTG_Framework.Data;

internal enum MovePersistenceFaultPoint
{
    AfterSerialize,
    BeforeStageFlush,
    AfterStage,
    AfterStagedValidation,
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
    private readonly Dictionary<string, string> _destinationIdentities = new(PathComparer);
    private readonly object _destinationIdentitySync = new();

    internal MoveDatasetPersistence(
        string root, DataStore store,
        Action<MovePersistenceFaultPoint>? fault = null,
        Action<string>? diagnostic = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
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
        RejectReparsePoints(_root, path);
        byte[] bytes = File.ReadAllBytes(path);
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

        var missingProfile = candidate.Moves.FirstOrDefault(move =>
            _store.GetKnockbackProfile(move.KnockbackProfileId) is null);
        if (missingProfile is not null)
        {
            var error = new MoveValidationError("knockback_profile_id", missingProfile.KnockbackProfileId,
                "Referenced KnockbackProfile does not exist.", "Select an existing knockback profile.");
            return new MoveSaveResult(MoveSaveStatus.ValidationFailed, candidate.SourceIdentity, Errors: new[] { error });
        }

        byte[] currentBytes = File.ReadAllBytes(destination);
        var currentIdentity = MoveContentIdentity.FromBytes(currentBytes);
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
            var stagedDocument = MoveDatasetCodec.Parse(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(staged)));
            _fault?.Invoke(MovePersistenceFaultPoint.AfterStagedValidation);
            ulong expectedDatasetVersion = _store.MoveDatasetVersion;
            bool won = _store.TryCommitMoveDataset(stagedDocument.Moves.ToArray(), expectedDatasetVersion, () =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;
                _fault?.Invoke(MovePersistenceFaultPoint.BeforeContainmentRecheck);
                RejectReparsePoints(_root, destination);
                var accessIdentity = MoveContentIdentity.FromBytes(File.ReadAllBytes(destination));
                if (accessIdentity != candidate.SourceIdentity)
                    return false;
                _fault?.Invoke(MovePersistenceFaultPoint.BeforeReplace);
                PhysicsDataPersistence.ReplaceStaged(staged, destination);
                committed = true;
                return true;
            });
            if (!won)
            {
                currentIdentity = MoveContentIdentity.FromBytes(File.ReadAllBytes(destination));
                return new MoveSaveResult(MoveSaveStatus.Conflict, candidate.SourceIdentity, currentIdentity);
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
            identifier.Contains('/') || identifier.Contains('\\') || identifier is "." or ".." ||
            identifier.Contains("..", StringComparison.Ordinal))
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

    private static void RejectReparsePoints(string root, string destination)
    {
        string? current = File.Exists(destination) ? destination : Path.GetDirectoryName(destination);
        while (current is not null && current.Length >= root.Length)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"[Data] Reparse points are not allowed in the move-data path: {current}");
            }
            if (string.Equals(current, root, PathComparison)) break;
            current = Path.GetDirectoryName(current);
        }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
