#nullable enable
using System;
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

    /// <summary>
    /// Reproduces direct engine calls performed on the recording side outside
    /// the event stream (e.g. FrameDataEngine.StartMove for MoveStartedEvent).
    /// Invoked at injection time, before the event is queued.
    /// </summary>
    Action<object>? OwnerApplier { get; set; }
}
