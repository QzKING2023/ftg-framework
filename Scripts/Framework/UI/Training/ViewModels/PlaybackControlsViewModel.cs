#nullable enable

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class PlaybackControlsViewModel
{
    public int DisplayFrame { get; private set; }
    public bool Paused { get; private set; }
    public string DisplayText { get; private set; } = "Frame: 0 [RUNNING]";

    public void OnFrameAdvanced(int frameNumber)
    {
        DisplayFrame = frameNumber;
        UpdateLabel();
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
        UpdateLabel();
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

    private void UpdateLabel()
    {
        string state = Paused ? "PAUSED" : "RUNNING";
        DisplayText = $"Frame: {DisplayFrame} [{state}]";
    }
}
