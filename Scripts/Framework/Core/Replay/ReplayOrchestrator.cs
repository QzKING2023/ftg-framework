#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using FTG_Framework.Core.Events;

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

        _recordingInitialSnapshot = _snapshotCoordinator is null
            ? null
            : StateSnapshotCodec.Encode(_snapshotCoordinator.Capture(
                Math.Max(0, EventBus.Instance.CurrentFrame - 1), "2.3.0"));
        _recorder.IsRecording = true;
        _snapshots.Clear();
        EventBus.Instance.Recorder = _recorder;
        FrameworkLog.Info?.Invoke("[Replay] Recording started.");
    }

    public ReplayFile StopRecording()
    {
        if (!_recorder.IsRecording)
            throw new InvalidOperationException("[Replay] Not currently recording.");

        _recorder.IsRecording = false;
        EventBus.Instance.Recorder = null;

        ReplayFile recorded = _recorder.Save();
        var file = new ReplayFile(recorded.FrameworkVersion, recorded.DataVersion,
            recorded.FrameCount, recorded.Entries, _recordingInitialSnapshot);
        _recordingInitialSnapshot = null;
        FrameworkLog.Info?.Invoke($"[Replay] Recording stopped: {file.EventCount} events over {file.FrameCount} frames.");
        return file;
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

        _playbackModes.Enter(RuntimePlaybackMode.AuthoritativeReplay, EventBus.Instance.LifecycleEpoch);
        IReplayRecorder? previousRecorder = EventBus.Instance.Recorder;
        bool previousSuppression = EventBus.Instance.SuppressFrameAdvanced;
        try
        {
            _player.Load(file);
            _loadedFile = file;
            _currentReplayPath = path;
            _replayFrame = 0;
            _preReplayFrameNumber = EventBus.Instance.CurrentFrame;

            EventBus.Instance.Recorder = null;
            EventBus.Instance.SuppressFrameAdvanced = true;
            EventBus.Instance.PublishImmediate(new ReplayStartedEvent(file.FrameCount, file.DataVersion));
            if (initialSnapshot is not null)
                _snapshotCoordinator!.Restore(initialSnapshot, SnapshotRestoreMode.ReplayBootstrap);
            _playbackModes.RebindEpoch(RuntimePlaybackMode.AuthoritativeReplay, EventBus.Instance.LifecycleEpoch);
            _player.IsPlaying = true;
            FrameworkLog.Info?.Invoke($"[Replay] Playback started: {file.EventCount} events, {file.FrameCount} frames.");
        }
        catch
        {
            _player.IsPlaying = false;
            _loadedFile = null;
            _currentReplayPath = null;
            _replayFrame = 0;
            EventBus.Instance.Recorder = previousRecorder;
            EventBus.Instance.SuppressFrameAdvanced = previousSuppression;
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

        EventBus.Instance.SuppressFrameAdvanced = false;

        StateSnapshot? finalSnapshot = _snapshotCoordinator is null
            ? null
            : _snapshotCoordinator.Capture(Math.Max(0, framesPlayed - 1), "2.3.0");
        try
        {
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
        finally { _playbackModes.Exit(RuntimePlaybackMode.AuthoritativeReplay); }
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
