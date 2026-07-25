#nullable enable
using System;
using FTG_Framework.Core;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class PlaybackControlsViewModel
{
    public IFrameDataEngine? FrameDataEngine { get; set; }

    // Syncs an external display (InputLog) with the pause/rewind frame window.
    // int.MaxValue means "no limit" (running or restored to live).
    public Action<int>? DisplayFrameLimitChanged { get; set; }

    public int DisplayFrame { get; private set; }
    public bool Paused { get; private set; }
    public string DisplayText { get; private set; } = "Frame: 0 [RUNNING]";

    public void OnFrameAdvanced(int frameNumber)
    {
        DisplayFrame = frameNumber;
        if (Paused)
            DisplayFrameLimitChanged?.Invoke(DisplayFrame);
        UpdateLabel();
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
        EventBus.Instance.Paused = paused;
        if (!paused)
            DisplayFrameLimitChanged?.Invoke(int.MaxValue);
        UpdateLabel();
    }

    public void TogglePause() => SetPaused(!Paused);

    public void StepForward()
    {
        if (!Paused)
            SetPaused(true);
        EventBus.Instance.StepRequested = true;
    }

    public void StepBackward()
    {
        if (!Paused)
            SetPaused(true);
        if (!CanStepBackward(FrameDataEngine?.EarliestSnapshotFrame ?? -1))
            return;
        if (FrameDataEngine is null)
            return;

        if (!FrameDataEngine.RestoreFrame(DisplayFrame - 1))
            return;

        OnStepBackward(DisplayFrame - 1);
        DisplayFrameLimitChanged?.Invoke(DisplayFrame);
    }

    public void OnStepBackward(int newFrame)
    {
        DisplayFrame = newFrame;
        UpdateLabel();
    }

    public bool CanStepBackward(int earliestSnapshotFrame)
    {
        return DisplayFrame > 0
            && (earliestSnapshotFrame < 0 || DisplayFrame - 1 >= earliestSnapshotFrame);
    }

    // Scene removal while paused must not leave the bus frozen for the next scene.
    public void OnExitTree()
    {
        if (!Paused)
            return;
        DisplayFrameLimitChanged?.Invoke(int.MaxValue);
        EventBus.Instance.Paused = false;
        EventBus.Instance.StepRequested = false;
        Paused = false;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        string state = Paused ? "PAUSED" : "RUNNING";
        DisplayText = $"Frame: {DisplayFrame} [{state}]";
    }
}
