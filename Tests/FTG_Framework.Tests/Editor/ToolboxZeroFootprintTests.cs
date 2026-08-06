#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// P-4.3/E4.3-C: zero-feature measurement after exactly-once close (S4.3-AC07/
/// AC13). The fixed measurement interval is 600 ProcessFrame() calls; the
/// EventBus collection runs this case in shuffled order under each seed of the
/// documented 2202-2206 range, so the measurement itself is seed-free and
/// deterministic. The allocation probe is the story-mandated warm-up pass
/// (60 frames) followed by GC.GetAllocatedBytesForCurrentThread deltas across
/// the interval; the post-close (closed-toolbox) delta must equal the
/// never-activated (no-feature) delta — zero feature allocations after close.
/// Interval and probe are recorded in evidence/v2-4-3/ (E4.3-C).
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class ToolboxZeroFootprintTests : IDisposable
{
    private const int WarmUpFrames = 60;
    private const int MeasurementFrames = 600; // P-4.3 fixed interval

    private readonly EventBusTestScope _eventBusScope = new();

    [Fact]
    public void AfterClose_ZeroSubscriptions_ZeroFeatureAllocationsOverFixedInterval()
    {
        // Baseline pass: the never-activated (no-feature) state.
        WarmUp();
        long noFeatureDelta = MeasureInterval();

        // Activate the full toolbox workspace (all three panel kinds) and close
        // it exactly once. The post-close state must be indistinguishable from
        // never-activated: zero workspace-owned subscriptions and identical
        // per-interval allocation.
        var workspace = new ToolboxWorkspaceService();
        ActivateAllPanels(workspace);
        workspace.Close();

        Assert.True(workspace.IsClosed);
        Assert.Equal(0, EventBus.Instance.GetSubscriberCount<MatchInitializedEvent>());
        Assert.Equal(0, EventBus.Instance.GetSubscriberCount<ReplayStartedEvent>());
        Assert.Equal(0, EventBus.Instance.GetSubscriberCount<ReplayEndedEvent>());
        Assert.Equal(0, EventBus.Instance.GetSubscriberCount<StateRestoredEvent>());

        // Closed-toolbox pass: identical warm-up and identical interval.
        WarmUp();
        long closedToolboxDelta = MeasureInterval();

        Assert.Equal(noFeatureDelta, closedToolboxDelta);
    }

    private static void ActivateAllPanels(ToolboxWorkspaceService workspace)
    {
        foreach (ToolboxPanelKind kind in new[]
                 {
                     ToolboxPanelKind.MoveAuthoring,
                     ToolboxPanelKind.EventBusDebug,
                     ToolboxPanelKind.RuntimeTuning
                 })
        {
            workspace.Activate(new ToolboxPanelHandle(kind)
            {
                OnSuspend = () => { },
                OnResume = () => { },
                OnShutdown = () => { },
                OnRevalidate = () => { }
            });
        }
    }

    private static void WarmUp()
    {
        for (int i = 0; i < WarmUpFrames; i++)
            EventBus.Instance.ProcessFrame();
    }

    private static long MeasureInterval()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < MeasurementFrames; i++)
            EventBus.Instance.ProcessFrame();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    public void Dispose() => _eventBusScope.Dispose();
}
