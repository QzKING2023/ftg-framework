#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Plays back recorded events frame by frame.
/// </summary>
public interface IReplayPlayer
{
    void Load(ReplayFile file);
    IReadOnlyList<ReplayEntry> GetEventsForFrame(int frameNumber);
    int FrameCount { get; }
    int TotalEvents { get; }
    bool IsPlaying { get; set; }
    int ProcessFrameReplay(EventBus bus, int frameNumber);
}
