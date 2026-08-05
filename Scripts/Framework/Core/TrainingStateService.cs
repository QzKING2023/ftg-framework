#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Data;

namespace FTG_Framework.Core;

/// <summary>
/// Capture/restore orchestration for training state (Story 2.5). Save captures
/// through the existing snapshot coordinator at the last completed frame of a
/// quiescent EventBus; restore reads and fully validates the candidate before
/// driving the coordinator's Prepare/Commit protocol. This class is the single
/// persistence authority for training snapshots.
/// </summary>
public sealed class TrainingStateService
{
    public const string FrameworkVersion = "2.3.0";
    private readonly StateSnapshotCoordinator _coordinator;
    private readonly Func<int> _currentFrame;
    private readonly Func<ulong> _currentEpoch;

    public string? LastSaveSha256 { get; private set; }
    public string? LastSaveFrameInfo { get; private set; }

    public sealed record TrainingSaveSlotInfo(
        int Frame, ulong SourceEpoch, int ComponentCount, string Sha256);

    public bool TryInspect(string sourcePath, out TrainingSaveSlotInfo? info, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        try
        {
            StateSnapshot snapshot = TrainingStatePersistence.Load(sourcePath, out string digest);
            info = new TrainingSaveSlotInfo(
                snapshot.Frame, snapshot.SourceEpoch, snapshot.Components.Count, digest);
            error = string.Empty;
            return true;
        }
        catch (TrainingStateLoadException ex)
        {
            info = null;
            error = ex.Message;
            return false;
        }
    }

    public TrainingStateService(StateSnapshotCoordinator coordinator,
        Func<int>? currentFrame = null,
        Func<ulong>? currentEpoch = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _currentFrame = currentFrame ?? (() => 0);
        _currentEpoch = currentEpoch ?? (() => 1);
    }

    public bool TrySave(string destinationPath, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        int frame = Math.Max(0, _currentFrame() - 1);
        ulong epoch = _currentEpoch();
        StateSnapshot snapshot;
        try
        {
            snapshot = _coordinator.Capture(frame, FrameworkVersion);
        }
        catch (SnapshotPrepareException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = $"[Save] Snapshot capture failed: {ex.Message}";
            return false;
        }
        TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, destinationPath);
        if (result.Status != TrainingSaveStatus.Succeeded)
        {
            error = result.Error ?? "[Save] Save failed without a diagnostic.";
            return false;
        }
        LastSaveSha256 = result.Sha256;
        LastSaveFrameInfo = $"frame {snapshot.Frame}, epoch {snapshot.SourceEpoch}, {snapshot.Components.Count} components";
        error = string.Empty;
        return true;
    }

    public bool TryRestore(string sourcePath, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        StateSnapshot snapshot;
        try
        {
            snapshot = TrainingStatePersistence.Load(sourcePath, out _);
        }
        catch (TrainingStateLoadException ex)
        {
            error = ex.Message;
            return false;
        }
        if (!string.Equals(snapshot.FrameworkVersion, FrameworkVersion, StringComparison.Ordinal))
        {
            error = $"[Load] Snapshot framework '{snapshot.FrameworkVersion}' is incompatible with '{FrameworkVersion}'; " +
                    "compatibility requires the same major/minor with an explicit, ordered, complete, tested migration — none exists for this version.";
            return false;
        }
        string? catalogError = ValidateComponentCatalog(snapshot);
        if (catalogError is not null)
        {
            error = catalogError;
            return false;
        }
        try
        {
            _coordinator.Restore(snapshot, SnapshotRestoreMode.Normal);
        }
        catch (SnapshotPrepareException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            error = $"[Load] Restore rejected before live mutation: {ex.Message}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static string? ValidateComponentCatalog(StateSnapshot snapshot)
    {
        var known = new HashSet<string>(
            SnapshotParticipantCatalog.Required.Concat(new[] { SnapshotParticipantCatalog.Combo, SnapshotParticipantCatalog.TrainingInput }),
            StringComparer.Ordinal);
        var actual = snapshot.Components.Select(component => component.Discriminator).ToHashSet(StringComparer.Ordinal);
        string[] missing = SnapshotParticipantCatalog.Required.Where(id => !actual.Contains(id)).ToArray();
        if (missing.Length != 0)
            return $"[Load] Candidate is missing required components: {string.Join(", ", missing)}.";
        string[] unknown = actual.Where(id => !known.Contains(id)).ToArray();
        if (unknown.Length != 0)
            return $"[Load] Candidate contains unknown components: {string.Join(", ", unknown)}.";
        return null;
    }
}
