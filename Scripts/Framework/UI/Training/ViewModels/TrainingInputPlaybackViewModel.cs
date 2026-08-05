#nullable enable
using System.Collections.Generic;
using System;
using FTG_Framework.Input;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class TrainingInputPlaybackViewModel
{
    private readonly TrainingInputRecordingLibrary _library;
    private readonly TrainingInputService? _service;
    private readonly Func<int> _currentFrame;
    private readonly Func<ulong> _currentEpoch;
    private string? _pendingCompletionStatus;

    public int SourcePlayer { get; set; } = 1;
    public int DummyPlayer { get; set; } = 2;
    public string? SelectedName { get; private set; }
    public string StatusText { get; private set; } = "Ready";
    public IReadOnlyDictionary<string, TrainingInputRecording> Recordings => _library.Recordings;
    public bool IsCapturing => _service?.IsCapturing == true;
    public bool IsPlaying => _service?.IsPlaying == true;
    public TrainingInputRecording? PendingRecording { get; private set; }
    public string RecordActionText => IsCapturing ? "Stop Recording" : "Start Recording";

    public TrainingInputPlaybackViewModel(TrainingInputRecordingLibrary library,
        TrainingInputService? service = null,
        Func<int>? currentFrame = null,
        Func<ulong>? currentEpoch = null)
    {
        _library = library;
        _service = service;
        _currentFrame = currentFrame ?? (() => 0);
        _currentEpoch = currentEpoch ?? (() => 1);
    }

    public bool AddRecording(TrainingInputRecording recording, bool confirmReplace)
    {
        bool accepted = _library.TryAddOrReplace(
            recording, confirmReplace, _service?.ActivePlaybackRecording, out string error);
        StatusText = accepted ? $"Recording '{recording.Name}' is available." : error;
        return accepted;
    }

    public bool Select(string name)
    {
        if (!_library.Recordings.ContainsKey(name))
        {
            StatusText = $"[Input] Recording '{name}' was not found.";
            return false;
        }
        SelectedName = name;
        StatusText = $"Selected '{name}'.";
        return true;
    }

    public bool AssignSelected()
    {
        if (SourcePlayer == DummyPlayer)
        {
            StatusText = "[Input] Source and dummy players must be different.";
            return false;
        }
        if (SelectedName is null)
        {
            StatusText = "[Input] Select a recording before assignment.";
            return false;
        }
        bool accepted = _library.TryAssign(DummyPlayer, SelectedName, out string error);
        StatusText = accepted ? $"Assigned '{SelectedName}' to P{DummyPlayer}." : error;
        return accepted;
    }

    public bool StartCapture()
    {
        if (_service is null)
        {
            StatusText = "[Input] Training input service is unavailable.";
            return false;
        }
        if (PendingRecording is not null)
        {
            StatusText = "[Input] Save/Overwrite or Cancel the pending recording before starting another capture.";
            return false;
        }
        bool accepted = _service.TryStartCapture(
            SourcePlayer, _currentFrame(), _currentEpoch(), out string error);
        StatusText = accepted ? $"Recording P{SourcePlayer} canonical input." : error;
        return accepted;
    }

    public bool StopCapture(string name, bool confirmReplace)
    {
        if (_service?.IsCapturing != true)
        {
            StatusText = "[Input] Capture is not active.";
            return false;
        }
        if (!TryValidateStorageName(name)) return false;
        if (!_service.TryStopCapture(_currentFrame(), string.Empty,
                out TrainingInputCaptureCompletion completion, out string error))
        {
            StatusText = error;
            return false;
        }
        return StageAndCommit(completion, name, confirmReplace);
    }

    public bool ToggleCapture(string name)
    {
        if (PendingRecording is not null)
        {
            StatusText = "[Input] Save/Overwrite or Cancel the pending recording before starting another capture.";
            return false;
        }
        return IsCapturing ? StopCapture(name, confirmReplace: false) : StartCapture();
    }

    public bool ConsumeServiceCompletion(string name)
    {
        if (_service is null) return false;
        bool hasCompletion = _service.HasCompletedCapture;
        if (_service.TryTakeOperationStatus(out string queuedStatus))
        {
            StatusText = queuedStatus;
            if (hasCompletion) _pendingCompletionStatus = queuedStatus;
        }
        if (!hasCompletion) return false;
        if (!TryValidateStorageName(name))
        {
            if (_pendingCompletionStatus is not null) StatusText = _pendingCompletionStatus;
            return false;
        }
        if (!_service.TryTakeCompletedCapture(out TrainingInputCaptureCompletion completion))
            return false;
        _pendingCompletionStatus = null;
        return StageAndCommit(completion, name, confirmReplace: false);
    }

    public bool ConfirmPendingOverwrite()
    {
        if (PendingRecording is null)
        {
            StatusText = "[Input] No recording replacement is pending.";
            return false;
        }
        bool accepted = _library.TryAddOrReplace(
            PendingRecording, confirmReplace: true, _service?.ActivePlaybackRecording, out string error);
        if (!accepted)
        {
            StatusText = error;
            return false;
        }
        string name = PendingRecording.Name;
        PendingRecording = null;
        StatusText = $"Replaced recording '{name}'.";
        return true;
    }

    public void CancelPending()
    {
        PendingRecording = null;
        StatusText = "Recording save canceled.";
    }

    public bool StartPlayback(bool loop)
    {
        if (_service is null)
        {
            StatusText = "[Input] Training input service is unavailable.";
            return false;
        }
        TrainingInputRecording? recording = _library.GetAssigned(DummyPlayer);
        if (recording is null)
        {
            StatusText = $"[Input] Assign a recording to P{DummyPlayer} before playback.";
            return false;
        }
        bool accepted = _service.TryStartPlayback(
            recording, DummyPlayer, _currentFrame(), loop, _currentEpoch(), out string error);
        StatusText = accepted
            ? $"Playing '{recording.Name}' on P{DummyPlayer}{(loop ? " in a loop" : string.Empty)}."
            : error;
        return accepted;
    }

    public void StopPlayback()
    {
        _service?.StopPlayback();
        StatusText = "Stopped training input playback.";
    }

    private bool TryValidateStorageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = "[Input] Recording name is required.";
            return false;
        }
        try
        {
            TrainingInputRecording.ValidateName(name);
            return true;
        }
        catch (TrainingInputRecordingException ex)
        {
            StatusText = ex.Message;
            return false;
        }
    }

    private bool StageAndCommit(TrainingInputCaptureCompletion completion,
        string name, bool confirmReplace)
    {
        TrainingInputRecording candidate;
        try
        {
            candidate = new TrainingInputRecording(
                completion.Recording.SchemaVersion,
                completion.Recording.SourcePlayer,
                completion.Recording.DurationFrames,
                name,
                completion.Recording.Entries);
        }
        catch (TrainingInputRecordingException ex)
        {
            PendingRecording = completion.Recording;
            StatusText = ex.Message;
            return false;
        }
        PendingRecording = candidate;
        bool accepted = _library.TryAddOrReplace(
            candidate, confirmReplace, _service?.ActivePlaybackRecording, out string error);
        if (!accepted)
        {
            StatusText = error;
            return false;
        }
        PendingRecording = null;
        StatusText = completion.Reason == TrainingInputCaptureCompletionReason.DurationLimit
            ? $"Saved recording '{candidate.Name}' after reaching the duration limit."
            : $"Saved recording '{candidate.Name}'.";
        return true;
    }
}
