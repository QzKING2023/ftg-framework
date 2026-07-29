#nullable enable

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Records events with frame-number tags for replay.
/// </summary>
public interface IReplayRecorder
{
    void Record<T>(int frame, T evt);
    ReplayFile Save();
    int MaxFrameNumber { get; }
    int EventCount { get; }
    bool IsRecording { get; set; }
}
