#nullable enable
using System;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Input;

[Collection(EventBusTestCollection.Name)]
public class InputHistoryRewindTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    private readonly InputHistory _history = new(capacity: 10);

    public void Dispose()
    {
        _history.Shutdown();
        _eventBusScope.Dispose();
    }

    [Fact]
    public void FrameRewound_TrimsEntriesBeyondRestorePoint()
    {
        EventBusTestHelper.Drain();
        int baseFrame = EventBus.Instance.CurrentFrame;

        for (int i = 0; i < 3; i++)
        {
            _history.RecordInput(1, InputType.Directional, 6);
            EventBus.Instance.ProcessFrame();
        }

        EventBus.Instance.PublishImmediate(new FrameRewoundEvent(baseFrame + 1));

        var frames = _history.GetDirectionalHistory(1);
        Assert.Equal(2, frames.Count);
        Assert.Equal(new[] { baseFrame, baseFrame + 1 }, frames.Select(e => e.Frame).ToArray());
    }

    // Re-executing a frame after a rewind must reproduce the original history
    // exactly — one entry per frame, no duplicates from the abandoned branch.
    [Fact]
    public void ReplayAfterRewind_LeavesNoDuplicateEntries()
    {
        EventBusTestHelper.Drain();
        int baseFrame = EventBus.Instance.CurrentFrame;

        for (int i = 0; i < 3; i++)
        {
            _history.RecordInput(1, InputType.Directional, 6);
            EventBus.Instance.ProcessFrame();
        }

        // Mirror the engine's restore flow: rewind event, then the bus counter
        // rewinds so the next processed frame re-executes as baseFrame+2.
        EventBus.Instance.PublishImmediate(new FrameRewoundEvent(baseFrame + 1));
        EventBus.Instance.RewindFrameCounter(baseFrame + 2);

        _history.RecordInput(1, InputType.Directional, 6);

        var frames = _history.GetDirectionalHistory(1);
        Assert.Equal(3, frames.Count);
        Assert.Equal(new[] { baseFrame, baseFrame + 1, baseFrame + 2 }, frames.Select(e => e.Frame).ToArray());
    }
}
