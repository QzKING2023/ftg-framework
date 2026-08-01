#nullable enable
using System;
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public class GameLoopPauseTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Flags);
    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    [Fact]
    public void Paused_ProcessFrameNotCalled()
    {
        EventBusTestHelper.Drain();

        // Drive the shipped gate, not a re-implemented expression: paused with
        // no step request must not process a frame.
        Assert.False(GameLoop.ShouldProcessFrame(paused: true, stepRequested: false));
    }

    [Fact]
    public void StepRequested_BypassesPauseForOneFrame()
    {
        EventBusTestHelper.Drain();

        Assert.True(GameLoop.ShouldProcessFrame(paused: true, stepRequested: true));

        // GameLoop clears StepRequested before the tick, so the bypass lasts
        // exactly one frame — simulate the tick it permits.
        EventBus.Instance.Paused = true;
        EventBus.Instance.StepRequested = false;
        int frameBefore = EventBus.Instance.CurrentFrame;
        EventBus.Instance.ProcessFrame();
        Assert.Equal(frameBefore + 1, EventBus.Instance.CurrentFrame);
    }
}

[Collection(EventBusTestCollection.Name)]
public class EventBusPauseTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Flags);
    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    [Fact]
    public void ProcessFrame_AlwaysProcessesOneFrame_RegardlessOfPaused()
    {
        EventBusTestHelper.Drain();
        EventBus.Instance.Paused = true;

        int frameBefore = EventBus.Instance.CurrentFrame;

        // ProcessFrame itself ignores Paused — it always processes one frame
        EventBus.Instance.ProcessFrame();

        Assert.Equal(frameBefore + 1, EventBus.Instance.CurrentFrame);
    }
}
