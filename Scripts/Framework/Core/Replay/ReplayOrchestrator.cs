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
internal sealed class ReplayOrchestrator
{
    private readonly IFrameDataEngine? _frameDataEngine;
    private readonly IReplayRecorder _recorder;
    private readonly IReplayPlayer _player;
    private readonly Dictionary<int, FrameStateSnapshot> _snapshots = new();

    private ReplayFile? _loadedFile;
    private int _replayFrame;
    private string? _currentReplayPath;
    private int _preReplayFrameNumber;

    public bool IsRecording => _recorder.IsRecording;
    public bool IsPlaying => _player.IsPlaying;
    public int CurrentReplayFrame => _replayFrame;
    public int TotalReplayFrames => _loadedFile?.FrameCount ?? 0;
    public string? CurrentReplayPath => _currentReplayPath;

    public ReplayOrchestrator(IFrameDataEngine? frameDataEngine = null)
    {
        _frameDataEngine = frameDataEngine;
        _recorder = new ReplayRecorder();
        _player = new ReplayPlayer();
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

        var file = _recorder.Save();
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
        var json = System.IO.File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = ReplayEventJsonContext.Default,
            PropertyNameCaseInsensitive = true
        };
        var file = JsonSerializer.Deserialize<ReplayFile>(json, options)
                   ?? throw new InvalidOperationException($"[Replay] Failed to deserialize replay file: {path}");

        ReplayVersionValidator.ValidateVersion(file.DataVersion);
        ReplayVersionValidator.ValidateFrameworkVersion(file.FrameworkVersion, "2.3.0");

        _loadedFile = file;
        _currentReplayPath = path;
        _player.Load(file);
        _player.IsPlaying = true;
        _replayFrame = 0;

        // Prevent double-recording of injected events and auto FrameAdvancedEvent
        EventBus.Instance.Recorder = null;
        EventBus.Instance.SuppressFrameAdvanced = true;

        // Save pre-replay frame number so we can restore it after replay ends
        _preReplayFrameNumber = EventBus.Instance.CurrentFrame;

        // Restore FrameDataEngine from snapshot at start frame-1 (if not frame 0)
        if (_replayFrame > 0 && _frameDataEngine is not null)
        {
            if (_snapshots.TryGetValue(_replayFrame - 1, out var snapshot))
                _frameDataEngine.RestoreFromReplaySnapshot(snapshot);
        }

        EventBus.Instance.Publish(new ReplayStartedEvent(file.FrameCount, file.DataVersion));
        FrameworkLog.Info?.Invoke($"[Replay] Playback started: {file.EventCount} events, {file.FrameCount} frames.");
    }

    public void Stop()
    {
        if (!_player.IsPlaying)
            return;

        _player.IsPlaying = false;
        int framesPlayed = _replayFrame;
        _replayFrame = 0;

        EventBus.Instance.SuppressFrameAdvanced = false;

        // Restore the frame counter to where it was before replay started.
        // Use RewindFrameCounter with the pre-replay value + 1 so the next
        // ProcessFrame continues from where live gameplay left off.
        EventBus.Instance.RewindFrameCounter(_preReplayFrameNumber);

        EventBus.Instance.Publish(new ReplayEndedEvent(framesPlayed));
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
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = ReplayEventJsonContext.Default,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(file, options);
        System.IO.File.WriteAllText(path, json);
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
