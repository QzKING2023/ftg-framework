#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Owns the replay lifecycle: recording, playback, and frame injection.
/// Pause/resume/step are handled through EventBus.Paused/StepRequested
/// (via PlaybackControlsViewModel), not through the orchestrator directly.
/// </summary>
internal sealed class ReplayOrchestrator : IStateSnapshotParticipant
{
    private readonly IFrameDataEngine? _frameDataEngine;
    private readonly IReplayRecorder _recorder;
    private readonly IReplayPlayer _player;
    private readonly Dictionary<int, FrameStateSnapshot> _snapshots = new();
    private readonly PlaybackModeCoordinator _playbackModes;
    private StateSnapshotCoordinator? _snapshotCoordinator;

    private ReplayFile? _loadedFile;
    private int _replayFrame;
    private string? _currentReplayPath;
    private int _preReplayFrameNumber;
    private byte[]? _recordingInitialSnapshot;
    private bool _stateScopedRecording;
    private ReplayFile? _pendingReplayWrite;

    public bool IsRecording => _recorder.IsRecording;
    public bool IsPlaying => _player.IsPlaying;
    public int CurrentReplayFrame => _replayFrame;
    public int TotalReplayFrames => _loadedFile?.FrameCount ?? 0;
    public string? CurrentReplayPath => _currentReplayPath;

    public ReplayOrchestrator(IFrameDataEngine? frameDataEngine = null,
        PlaybackModeCoordinator? playbackModes = null,
        StateSnapshotCoordinator? snapshotCoordinator = null)
    {
        _frameDataEngine = frameDataEngine;
        _playbackModes = playbackModes ?? new PlaybackModeCoordinator();
        _snapshotCoordinator = snapshotCoordinator;
        _recorder = new ReplayRecorder();
        _player = new ReplayPlayer();
        _player.OwnerApplier = ApplyOwnerState;
    }

    // Reproduces direct StartMove calls performed on the recording side outside
    // the event stream, so the FrameDataEngine state lands at the same frame as
    // recorded (hash@k). The Idle guard inside StartMove keeps the call
    // idempotent when a cancel chain already placed the engine in a phase.
    private void ApplyOwnerState(object evt)
    {
        if (_frameDataEngine is null || evt is not MoveStartedEvent started)
            return;
        _frameDataEngine.StartMove(started.PlayerId, started.MoveId);
    }

    internal void AttachSnapshotCoordinator(StateSnapshotCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        if (_snapshotCoordinator is not null && !ReferenceEquals(_snapshotCoordinator, coordinator))
            throw new InvalidOperationException("[Replay] Snapshot coordinator is already attached.");
        _snapshotCoordinator = coordinator;
    }

    string IStateSnapshotParticipant.Discriminator => SnapshotParticipantCatalog.Recording;
    int IStateSnapshotParticipant.CodecVersion => 1;
    SnapshotComponent IStateSnapshotParticipant.Capture(int frame, ulong epoch) => new(
        SnapshotParticipantCatalog.Recording, 1,
        JsonSerializer.Serialize(new RecordingRuntimeSnapshot(_recorder.IsRecording)));

    IPreparedSnapshotComponent IStateSnapshotParticipant.Prepare(
        SnapshotComponent component, SnapshotPrepareContext context)
    {
        RecordingRuntimeSnapshot decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<RecordingRuntimeSnapshot>(component.Payload)
                ?? throw new SnapshotPrepareException(SnapshotParticipantCatalog.Recording, "Codec returned null.");
        }
        catch (JsonException ex)
        {
            throw new SnapshotPrepareException(SnapshotParticipantCatalog.Recording, "Invalid payload.", ex);
        }
        var prepared = context.Mode == SnapshotRestoreMode.Normal
            ? decoded
            : new RecordingRuntimeSnapshot(false);
        return PreparedSnapshotComponent.CreateOwnerSwap(
            SnapshotParticipantCatalog.Recording, prepared, InstallRecordingSnapshot);
    }

    private void InstallRecordingSnapshot(RecordingRuntimeSnapshot snapshot)
    {
        _recorder.IsRecording = snapshot.IsRecording;
        EventBus.Instance.Recorder = snapshot.IsRecording ? _recorder : null;
    }

    // ─── Recording ────────────────────────────────────────────────

    public void StartRecording()
    {
        if (_recorder.IsRecording)
            return;

        if (_player.IsPlaying)
        {
            FrameworkLog.Error?.Invoke("[Replay] Cannot start recording during playback.");
            return;
        }

        _stateScopedRecording = false;
        _recordingInitialSnapshot = _snapshotCoordinator is null
            ? null
            : StateSnapshotCodec.Encode(_snapshotCoordinator.Capture(
                Math.Max(0, EventBus.Instance.CurrentFrame - 1), "2.3.0"));
        _recorder.IsRecording = true;
        _snapshots.Clear();
        EventBus.Instance.Recorder = _recorder;
        FrameworkLog.Info?.Invoke("[Replay] Recording started.");
    }

    /// <summary>
    /// State-scoped recording entry (S4.2-B, S4.2-AC01/AC05): restores the
    /// selected Story 2.5 save through the non-observable ReplayBootstrap path
    /// under one fresh reserved epoch (no StateRestored), then attaches the
    /// authoritative recorder so the first envelope lands at snapshot.frame + 1.
    /// Every fallible step (file read, container verification, decode, graph
    /// pre-validation, ownership checks) precedes the restore commit (S4.2-AC13);
    /// rejection leaves live state, epoch, queues, and the save file unchanged
    /// (S4.2-AC09).
    /// </summary>
    public bool TryStartStateScopedRecording(string snapshotPath, out string error)
    {
        if (_recorder.IsRecording)
        {
            error = "[Replay] A recording is already active; state-scoped recording is exactly-once.";
            return false;
        }
        if (_player.IsPlaying)
        {
            error = "[Replay] Authoritative replay is active; state-scoped recording cannot share the session.";
            return false;
        }
        if (_playbackModes.ActiveMode != RuntimePlaybackMode.None)
        {
            error = $"[Replay] {_playbackModes.ActiveMode} owns the session; state-scoped recording cannot begin.";
            return false;
        }
        if (_playbackModes.CapturePlayer != 0)
        {
            error = $"[Replay] P{_playbackModes.CapturePlayer} capture is active; state-scoped recording cannot begin.";
            return false;
        }
        if (_snapshotCoordinator is null)
        {
            error = "[Replay] State-scoped recording requires a runtime snapshot coordinator.";
            return false;
        }

        byte[] fileBytes;
        try { fileBytes = File.ReadAllBytes(snapshotPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = $"[Replay] Cannot read snapshot save file: {ex.Message}";
            return false;
        }
        // S4.2-AC13: bound the candidate save before any validation or lifecycle
        // mutation; the persistence boundary enforces the same limit on write.
        if (fileBytes.Length > TrainingStatePersistence.MaxFileBytes)
        {
            error = $"[Replay] Snapshot save file exceeds the {TrainingStatePersistence.MaxFileBytes}-byte P-SAVE limit.";
            return false;
        }
        byte[] containerBytes;
        try
        {
            containerBytes = TrainingStatePersistence.ExtractVerifiedContainer(fileBytes, out _);
        }
        catch (Exception ex) when (ex is TrainingStateLoadException or InvalidDataException or FormatException)
        {
            error = $"[Replay] Snapshot save file failed integrity verification: {ex.Message}";
            return false;
        }
        StateSnapshot snapshot;
        try
        {
            snapshot = StateSnapshotCodec.Decode(containerBytes);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FormatException)
        {
            error = $"[Replay] Snapshot container is invalid: {ex.Message}";
            return false;
        }
        // S4.2-AC03/AC04: the declared identity/hash and the cross-component
        // graph are validated before any lifecycle mutation. The verified
        // container bytes are embedded as-is (no re-encode) so the stamped hash
        // is byte-exact against the save document's SHA-256.
        try
        {
            _snapshotCoordinator.ValidateSnapshot(snapshot, SnapshotRestoreMode.ReplayBootstrap);
        }
        catch (SnapshotPrepareException ex)
        {
            error = $"[Replay] Snapshot failed graph validation: {ex.Message}";
            return false;
        }

        // The committed swap: non-observable restore under one fresh epoch.
        try
        {
            _snapshotCoordinator.Restore(snapshot, SnapshotRestoreMode.ReplayBootstrap);
        }
        catch (Exception ex) when (ex is SnapshotPrepareException or InvalidOperationException)
        {
            error = $"[Replay] State-scoped bootstrap restore failed: {ex.Message}";
            return false;
        }

        // Attach the recorder AFTER the restore: the orchestrator's own snapshot
        // participant installs RecordingRuntimeSnapshot(false) for non-Normal
        // modes, resetting EventBus.Recorder during the swap.
        _recordingInitialSnapshot = containerBytes;
        _stateScopedRecording = true;
        _snapshots.Clear();
        _recorder.IsRecording = true;
        EventBus.Instance.Recorder = _recorder;
        FrameworkLog.Info?.Invoke(
            $"[Replay] State-scoped recording started from snapshot frame {snapshot.Frame}; resumes at frame {snapshot.Frame + 1}.");
        error = string.Empty;
        return true;
    }

    public ReplayFile StopRecording()
    {
        if (!_recorder.IsRecording)
            throw new InvalidOperationException("[Replay] Not currently recording.");

        _recorder.IsRecording = false;
        EventBus.Instance.Recorder = null;

        ReplayFile recorded = _recorder.Save();
        var file = new ReplayFile(recorded.FrameworkVersion, recorded.DataVersion,
            recorded.FrameCount, recorded.Entries, _recordingInitialSnapshot,
            _recordingInitialSnapshot is null ? null : ReplayFile.ComputeInitialSnapshotHash(_recordingInitialSnapshot),
            _stateScopedRecording);
        _recordingInitialSnapshot = null;
        _stateScopedRecording = false;
        FrameworkLog.Info?.Invoke($"[Replay] Recording stopped: {file.EventCount} events over {file.FrameCount} frames.");
        return file;
    }

    /// <summary>
    /// Stop-and-persist with retry (S4.2-AC13 stop path): StopRecording is the
    /// one-shot committed swap, so a failed Write must not strand the produced
    /// file — the ReplayFile is retained and a retry rewrites it without
    /// re-detaching.
    /// </summary>
    public bool TryStopAndWriteReplay(string replayPath, out string error)
    {
        try
        {
            if (_recorder.IsRecording)
                _pendingReplayWrite = StopRecording();
            else if (_pendingReplayWrite is null)
            {
                error = "[Replay] No active recording to stop.";
                return false;
            }
            ReplayCodec.Write(replayPath, _pendingReplayWrite);
            _pendingReplayWrite = null;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Called each frame AFTER ProcessFrame() during recording to save FrameDataEngine snapshots.
    /// </summary>
    public void CaptureSnapshot(int frameNumber)
    {
        if (!_recorder.IsRecording || _frameDataEngine is null)
            return;

        var snapshot = _frameDataEngine.TryGetSnapshot(frameNumber);
        if (snapshot.HasValue)
            _snapshots[frameNumber] = snapshot.Value;
    }

    // ─── Playback ─────────────────────────────────────────────────

    public void LoadAndStartReplay(string path)
    {
        var file = ReplayCodec.Read(path, "2.3.0");

        ReplayVersionValidator.ValidateVersion(file.DataVersion);
        ReplayVersionValidator.ValidateFrameworkVersion(file.FrameworkVersion, "2.3.0");

        StateSnapshot? initialSnapshot = file.InitialSnapshot is null
            ? null
            : StateSnapshotCodec.Decode(file.InitialSnapshot);
        if (initialSnapshot is not null && _snapshotCoordinator is null)
            throw new InvalidOperationException("[Replay] This replay requires a runtime snapshot coordinator.");
        // S4.2-AC04: graph pre-validation before acquiring replay ownership so a
        // rejected candidate performs no lifecycle mutation at all.
        if (initialSnapshot is not null)
            _snapshotCoordinator!.ValidateSnapshot(initialSnapshot, SnapshotRestoreMode.ReplayBootstrap);

        _playbackModes.Enter(RuntimePlaybackMode.AuthoritativeReplay, EventBus.Instance.LifecycleEpoch);
        IReplayRecorder? previousRecorder = EventBus.Instance.Recorder;
        bool previousSuppression = EventBus.Instance.SuppressFrameAdvanced;
        try
        {
            _player.Load(file);
            _loadedFile = file;
            _currentReplayPath = path;
            // S4.2-C: state-scoped files (format marker) carry an absolute frame
            // domain starting at snapshot.frame + 1; ordinary recordings and
            // legacy files start at frame 0.
            _replayFrame = file.StateScoped ? initialSnapshot!.Frame + 1 : 0;
            _preReplayFrameNumber = EventBus.Instance.CurrentFrame;

            EventBus.Instance.Recorder = null;
            EventBus.Instance.SuppressFrameAdvanced = true;
            if (initialSnapshot is not null)
                _snapshotCoordinator!.Restore(initialSnapshot, SnapshotRestoreMode.ReplayBootstrap);
            // ReplayStartedEvent is a lifecycle event: publishing it bumps the
            // epoch and clears stale queues, so it must wait until the restore
            // commit succeeds — a failed restore then leaves epoch and queues
            // unchanged (S4.2-AC09). Published before BeginReplayApply so the
            // session suppression does not swallow it.
            EventBus.Instance.PublishImmediate(new ReplayStartedEvent(file.FrameCount, file.DataVersion));
            // Session-level replay apply: suppresses derived publication of all
            // registered types for the whole playback session so injected events
            // execute exactly once (owner-applier + queue dispatch), never
            // re-broadcast by cancel-chain or frame-update subscribers.
            EventBus.Instance.BeginReplayApply();
            _playbackModes.RebindEpoch(RuntimePlaybackMode.AuthoritativeReplay, EventBus.Instance.LifecycleEpoch);
            _player.IsPlaying = true;
            FrameworkLog.Info?.Invoke($"[Replay] Playback started: {file.EventCount} events, {file.FrameCount} frames.");
        }
        catch
        {
            EventBus.Instance.EndReplayApply();
            _player.IsPlaying = false;
            _loadedFile = null;
            _currentReplayPath = null;
            _replayFrame = 0;
            EventBus.Instance.Recorder = previousRecorder;
            EventBus.Instance.SuppressFrameAdvanced = previousSuppression;
            // Exit throws on mode mismatch; guard so cleanup cannot mask the
            // original failure.
            if (_playbackModes.ActiveMode == RuntimePlaybackMode.AuthoritativeReplay)
                _playbackModes.Exit(RuntimePlaybackMode.AuthoritativeReplay);
            throw;
        }
    }

    public void Stop()
    {
        if (!_player.IsPlaying)
            return;

        _player.IsPlaying = false;
        int framesPlayed = _replayFrame;
        _replayFrame = 0;

        // End the session-level suppression BEFORE publishing ReplayEndedEvent,
        // which must reach its UI observers on the live epoch.
        EventBus.Instance.EndReplayApply();
        EventBus.Instance.SuppressFrameAdvanced = false;

        // S4.2 failure path (P4): every fallible step between detach and the
        // mode exit must sit inside the try so a mid-stop exception cannot
        // strand AuthoritativeReplay; Exit is guarded because it throws on mode
        // mismatch and must not mask the original failure.
        StateSnapshot? finalSnapshot = null;
        try
        {
            finalSnapshot = _snapshotCoordinator is null
                ? null
                : _snapshotCoordinator.Capture(Math.Max(0, framesPlayed - 1), "2.3.0");
            EventBus.Instance.PublishImmediate(new ReplayEndedEvent(framesPlayed));
            if (finalSnapshot is not null)
                _snapshotCoordinator!.Restore(finalSnapshot, SnapshotRestoreMode.ReplayHandoff);
            else
            {
                // Legacy playback without AD-20 participants resumes the live
                // frame stream from the point where replay took ownership.
                EventBus.Instance.RewindFrameCounter(_preReplayFrameNumber);
            }
        }
        finally
        {
            if (_playbackModes.ActiveMode == RuntimePlaybackMode.AuthoritativeReplay)
                _playbackModes.Exit(RuntimePlaybackMode.AuthoritativeReplay);
        }
        FrameworkLog.Info?.Invoke($"[Replay] Playback ended after {framesPlayed} frames.");
    }

    /// <summary>
    /// Called each frame during playback. Injects recorded events for the current frame
    /// and advances the frame counter. Returns true if playback is still active.
    /// </summary>
    public bool ProcessReplayFrame()
    {
        if (!_player.IsPlaying)
            return false;

        if (_loadedFile is null)
            return false;

        if (_replayFrame >= _loadedFile.FrameCount)
        {
            Stop();
            return false;
        }

        int dispatched = _player.ProcessFrameReplay(EventBus.Instance, _replayFrame);
        _replayFrame++;

        if (_replayFrame >= _loadedFile.FrameCount)
        {
            Stop();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Saves a replay file to disk as JSON.
    /// </summary>
    public void SaveReplay(string path)
    {
        var file = StopRecording();
        ReplayCodec.Write(path, file);
        FrameworkLog.Info?.Invoke($"[Replay] Replay saved to: {path}");
    }

    /// <summary>
    /// Loads snapshot data from a recorded session. Called before starting replay
    /// when replaying from snapshotted state.
    /// </summary>
    public void SetSnapshots(Dictionary<int, FrameStateSnapshot> snapshots)
    {
        _snapshots.Clear();
        foreach (var kvp in snapshots)
            _snapshots[kvp.Key] = kvp.Value;
    }
}
