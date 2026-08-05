#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Replay;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Input;

public enum TrainingInputCaptureCompletionReason
{
    ExplicitStop,
    DurationLimit
}

public readonly record struct TrainingInputCaptureCompletion(
    TrainingInputRecording Recording,
    TrainingInputCaptureCompletionReason Reason,
    ulong Epoch);

public sealed class TrainingInputService
{
    private readonly Action<int, InputType, int> _injectCanonical;
    private readonly Action<int, IReadOnlyList<TrainingInputRecordingEntry>>? _injectCanonicalBatch;
    private readonly Action<int, int> _resetTransient;
    private readonly PlaybackModeCoordinator _playbackModes;
    private readonly List<TrainingInputRecordingEntry> _captureEntries = new();
    private int _capturePlayer;
    private int _captureStartFrame;
    private int _captureLastFrame = -1;
    private int _captureSequence;
    private ulong _captureEpoch;
    private TrainingInputCaptureCompletion? _completedCapture;
    private string? _operationStatus;

    private TrainingInputRecording? _playback;
    private int _playbackPlayer;
    private int _playbackStartFrame;
    private int _playbackIndex;
    private bool _loop;
    private ulong _playbackEpoch;
    private bool _pendingLoopReset;
    private bool _pendingStop;
    private bool _lifecycleSubscribed;

    public bool IsCapturing => _capturePlayer != 0;
    public int CapturePlayer => _capturePlayer;
    public bool IsPlaying => _playback is not null;
    public int PlaybackPlayer => _playbackPlayer;
    public TrainingInputRecording? ActivePlaybackRecording => _playback;
    public bool HasCompletedCapture => _completedCapture.HasValue;

    public TrainingInputService(Action<int, InputType, int> injectCanonical,
        PlaybackModeCoordinator? playbackModes = null,
        Action<int, int>? resetTransient = null,
        Action<int, IReadOnlyList<TrainingInputRecordingEntry>>? injectCanonicalBatch = null)
    {
        ArgumentNullException.ThrowIfNull(injectCanonical);
        _injectCanonical = injectCanonical;
        _playbackModes = playbackModes ?? new PlaybackModeCoordinator();
        _resetTransient = resetTransient ?? ((_, _) => { });
        _injectCanonicalBatch = injectCanonicalBatch;
    }

    public bool TryStartCapture(int playerId, int startFrame, out string error)
        => TryStartCapture(playerId, startFrame, 1, out error);

    public bool TryStartCapture(int playerId, int startFrame, ulong epoch, out string error)
    {
        if (playerId is < 1 or > 2 || startFrame < 0 || epoch == 0)
        {
            error = "[Input] Capture requires player 1 or 2, a non-negative start, and an active epoch.";
            return false;
        }
        if (IsCapturing)
        {
            error = $"[Input] P{_capturePlayer} capture is already active.";
            return false;
        }
        if (_completedCapture.HasValue)
        {
            error = "[Input] Save or cancel the completed recording before starting another capture.";
            return false;
        }
        if (IsPlaying && _playbackPlayer == playerId)
        {
            error = $"[Input] P{playerId} is owned by training playback.";
            return false;
        }
        if (!_playbackModes.TryEnterCapture(playerId, epoch, out error))
            return false;
        _captureEntries.Clear();
        _capturePlayer = playerId;
        _captureStartFrame = startFrame;
        _captureLastFrame = -1;
        _captureSequence = 0;
        _captureEpoch = epoch;
        error = string.Empty;
        return true;
    }

    public TrainingInputRecording StopCapture(int stopFrame, string? name)
    {
        if (!TryStopCapture(stopFrame, name, out TrainingInputCaptureCompletion completion, out string error))
            throw new TrainingInputRecordingException(error);
        return completion.Recording;
    }

    public bool TryStopCapture(int stopFrame, string? name,
        out TrainingInputCaptureCompletion completion, out string error)
    {
        completion = default;
        if (!IsCapturing)
        {
            error = "[Input] Capture is not active.";
            return false;
        }
        if (stopFrame < _captureStartFrame)
        {
            error = "[Input] Capture stop frame cannot be before its start frame.";
            return false;
        }
        try
        {
            int duration = checked(stopFrame - _captureStartFrame);
            if (duration > TrainingInputRecording.MaxDurationFrames)
            {
                error = $"[Input] Capture duration exceeds {TrainingInputRecording.MaxDurationFrames} frames.";
                return false;
            }
            var recording = new TrainingInputRecording(1, _capturePlayer, duration, name, _captureEntries);
            completion = new TrainingInputCaptureCompletion(
                recording, TrainingInputCaptureCompletionReason.ExplicitStop, _captureEpoch);
        }
        catch (Exception ex) when (ex is OverflowException or TrainingInputRecordingException)
        {
            error = ex is TrainingInputRecordingException ? ex.Message : "[Input] Capture duration overflow.";
            return false;
        }
        ReleaseCapture();
        error = string.Empty;
        return true;
    }

    public void CompleteCaptureFrame(int completedFrame)
    {
        if (!IsCapturing) return;
        int relative;
        try { relative = checked(completedFrame - _captureStartFrame); }
        catch (OverflowException)
        {
            CancelCaptureWithStatus("[Input] Capture frame arithmetic overflow.");
            return;
        }
        if (relative < TrainingInputRecording.MaxDurationFrames) return;
        try
        {
            var recording = new TrainingInputRecording(
                1, _capturePlayer, TrainingInputRecording.MaxDurationFrames, string.Empty, _captureEntries);
            _completedCapture = new TrainingInputCaptureCompletion(
                recording, TrainingInputCaptureCompletionReason.DurationLimit, _captureEpoch);
            _operationStatus = $"[Input] Recording reached the {TrainingInputRecording.MaxDurationFrames}-frame limit and stopped.";
            ReleaseCapture();
        }
        catch (TrainingInputRecordingException ex)
        {
            CancelCaptureWithStatus(ex.Message);
        }
    }

    public bool TryTakeCompletedCapture(out TrainingInputCaptureCompletion completion)
    {
        if (_completedCapture is not TrainingInputCaptureCompletion value)
        {
            completion = default;
            return false;
        }
        completion = value;
        _completedCapture = null;
        return true;
    }

    public bool TryTakeOperationStatus(out string status)
    {
        if (_operationStatus is null)
        {
            status = string.Empty;
            return false;
        }
        status = _operationStatus;
        _operationStatus = null;
        return true;
    }

    public void AcceptCanonicalInput(int playerId, InputType type, int value, int frame)
    {
        ValidateCanonical(playerId, type, value, frame);
        if (IsCapturing && playerId == _capturePlayer)
        {
            int relative;
            try { relative = checked(frame - _captureStartFrame); }
            catch (OverflowException ex)
            {
                throw new TrainingInputRecordingException("[Input] Relative frame overflow.", ex);
            }
            if (relative < 0)
                throw new TrainingInputRecordingException("[Input] Canonical input predates capture start.");
            if (relative > TrainingInputRecording.MaxDurationFrames)
            {
                _injectCanonical(playerId, type, value);
                return;
            }
            if (frame != _captureLastFrame)
            {
                _captureLastFrame = frame;
                _captureSequence = 0;
            }
            if (_captureEntries.Count >= TrainingInputRecording.MaxEntries)
                CancelCaptureWithStatus($"[Input] Entry count exceeds {TrainingInputRecording.MaxEntries}; recording canceled.");
            else
            {
                _captureEntries.Add(new TrainingInputRecordingEntry(relative, _captureSequence, type, value));
                _captureSequence = checked(_captureSequence + 1);
            }
        }
        _injectCanonical(playerId, type, value);
    }

    public bool TryStartPlayback(TrainingInputRecording recording, int dummyPlayer,
        int startFrame, bool loop, ulong epoch, out string error)
    {
        ArgumentNullException.ThrowIfNull(recording);
        if (dummyPlayer is < 1 or > 2 || startFrame < 0 || epoch == 0)
        {
            error = "[Input] Playback requires player 1 or 2, a non-negative start, and an active epoch.";
            return false;
        }
        if (loop && (recording.DurationFrames == 0 || recording.Entries.Count == 0))
        {
            error = "[Input] Empty or zero-duration recordings cannot loop.";
            return false;
        }
        try
        {
            _ = checked(startFrame + recording.DurationFrames);
            if (loop) _ = checked(startFrame + recording.DurationFrames + 1);
            foreach (var entry in recording.Entries) _ = checked(startFrame + entry.RelativeFrame);
        }
        catch (OverflowException)
        {
            error = "[Input] Playback schedule overflow.";
            return false;
        }
        if (IsPlaying)
        {
            error = $"[Input] P{_playbackPlayer} playback is already active.";
            return false;
        }
        if (IsCapturing && _capturePlayer == dummyPlayer)
        {
            error = $"[Input] P{dummyPlayer} is owned by capture.";
            return false;
        }
        try { _playbackModes.Enter(RuntimePlaybackMode.TrainingInput, epoch); }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
        _playback = recording;
        _playbackPlayer = dummyPlayer;
        _playbackStartFrame = startFrame;
        _playbackIndex = 0;
        _loop = loop;
        _playbackEpoch = epoch;
        _pendingLoopReset = false;
        _pendingStop = false;
        error = string.Empty;
        return true;
    }

    public int ProcessPlaybackFrame(int currentFrame, ulong epoch)
    {
        if (_playback is null) return 0;
        if (epoch != _playbackEpoch)
        {
            ReleasePlayback(resetTransient: false);
            return 0;
        }
        int relative;
        try { relative = checked(currentFrame - _playbackStartFrame); }
        catch (OverflowException)
        {
            _operationStatus = "[Input] Playback frame arithmetic overflow; playback stopped.";
            ReleasePlayback(resetTransient: true);
            return 0;
        }
        if (relative < 0) return 0;
        int first = _playbackIndex;
        while (_playbackIndex < _playback.Entries.Count &&
               _playback.Entries[_playbackIndex].RelativeFrame == relative)
        {
            _playbackIndex++;
        }
        int injected = _playbackIndex - first;
        if (injected > 0)
        {
            var due = new TrainingInputRecordingEntry[injected];
            for (int i = 0; i < injected; i++) due[i] = _playback.Entries[first + i];
            if (_injectCanonicalBatch is not null) _injectCanonicalBatch(_playbackPlayer, due);
            else foreach (var entry in due) _injectCanonical(_playbackPlayer, entry.InputType, entry.InputValue);
        }
        if (relative >= _playback.DurationFrames)
        {
            if (_loop)
                _pendingLoopReset = true;
            else
                _pendingStop = true;
        }
        return injected;
    }

    public void CompleteFrame()
    {
        if (_playback is null) return;
        if (_pendingLoopReset)
        {
            int nextStart;
            try { nextStart = checked(_playbackStartFrame + _playback.DurationFrames + 1); }
            catch (OverflowException)
            {
                _operationStatus = "[Input] Loop schedule exhausted the frame range; playback stopped.";
                ReleasePlayback(resetTransient: true);
                return;
            }
            ResetPlaybackTransient();
            _playbackStartFrame = nextStart;
            _playbackIndex = 0;
            _pendingLoopReset = false;
        }
        if (_pendingStop) ReleasePlayback(resetTransient: true);
    }

    public void StopPlayback()
    {
        ReleasePlayback(resetTransient: true);
    }

    private void ReleasePlayback(bool resetTransient)
    {
        if (_playback is null) return;
        if (resetTransient) ResetPlaybackTransient();
        if (_playbackModes.ActiveMode == RuntimePlaybackMode.TrainingInput)
            _playbackModes.Exit(RuntimePlaybackMode.TrainingInput);
        _playback = null;
        _playbackPlayer = 0;
        _playbackStartFrame = 0;
        _playbackIndex = 0;
        _loop = false;
        _playbackEpoch = 0;
        _pendingLoopReset = false;
        _pendingStop = false;
    }

    public void Shutdown()
    {
        CancelForLifecycle();
        if (_lifecycleSubscribed)
        {
            EventBus.Instance.Unsubscribe<SceneChangingEvent>(OnSceneChanging);
            EventBus.Instance.Unsubscribe<MatchInitializedEvent>(OnMatchInitialized);
            EventBus.Instance.Unsubscribe<StateRestoredEvent>(OnStateRestored);
            EventBus.Instance.Unsubscribe<ReplayStartedEvent>(OnReplayStarted);
            EventBus.Instance.Unsubscribe<ReplayEndedEvent>(OnReplayEnded);
            _lifecycleSubscribed = false;
        }
    }

    public void StartLifecycleSubscriptions()
    {
        if (_lifecycleSubscribed) return;
        EventBus.Instance.Subscribe<SceneChangingEvent>(OnSceneChanging);
        EventBus.Instance.Subscribe<MatchInitializedEvent>(OnMatchInitialized);
        EventBus.Instance.Subscribe<StateRestoredEvent>(OnStateRestored);
        EventBus.Instance.Subscribe<ReplayStartedEvent>(OnReplayStarted);
        EventBus.Instance.Subscribe<ReplayEndedEvent>(OnReplayEnded);
        _lifecycleSubscribed = true;
    }

    public void CancelForLifecycle()
    {
        ReleasePlayback(resetTransient: false);
        _completedCapture = null;
        _operationStatus = null;
        if (_capturePlayer == 0) return;
        ReleaseCapture();
    }

    private void OnSceneChanging(SceneChangingEvent _) => CancelForLifecycle();
    private void OnMatchInitialized(MatchInitializedEvent _) => CancelForLifecycle();
    private void OnStateRestored(StateRestoredEvent _) => CancelForLifecycle();
    private void OnReplayStarted(ReplayStartedEvent _) => CancelForLifecycle();
    private void OnReplayEnded(ReplayEndedEvent _) => CancelForLifecycle();

    private void ResetPlaybackTransient()
    {
        _resetTransient(_playbackPlayer, _playbackStartFrame);
        _injectCanonical(_playbackPlayer, InputType.Directional, (int)DirectionValue.Neutral);
    }

    private void ReleaseCapture()
    {
        if (_capturePlayer == 0) return;
        int player = _capturePlayer;
        _capturePlayer = 0;
        _captureEntries.Clear();
        _captureStartFrame = 0;
        _captureLastFrame = -1;
        _captureSequence = 0;
        _captureEpoch = 0;
        _playbackModes.ExitCapture(player);
    }

    private void CancelCaptureWithStatus(string status)
    {
        _operationStatus = status;
        ReleaseCapture();
    }

    private static void ValidateCanonical(int playerId, InputType type, int value, int frame)
    {
        if (playerId is < 1 or > 2 || frame < 0)
            throw new TrainingInputRecordingException("[Input] Canonical input player/frame is invalid.");
        if (!Enum.IsDefined(type) ||
            type == InputType.Directional && !Enum.IsDefined(typeof(DirectionValue), value) ||
            type == InputType.Button && !Enum.IsDefined(typeof(ButtonValue), value))
            throw new TrainingInputRecordingException("[Input] Canonical input type/value is invalid.");
    }
}
