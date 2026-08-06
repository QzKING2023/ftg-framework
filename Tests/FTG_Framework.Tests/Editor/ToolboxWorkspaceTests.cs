#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// S4.3-A/AC10: workspace orchestration is a pure-C# service testable under
/// dotnet test. S4.3-AC12/AC13: epoch capture at activation, revalidation at
/// every lifecycle callback, exactly-once close, and invalidate-before-release.
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class ToolboxWorkspaceTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private readonly EventBus _bus = EventBus.Instance;

    public void Dispose() => _eventBusScope.Dispose();

    [Fact]
    public void Activate_CapturesEpochAndSubscribesLifecycleEvents()
    {
        using var workspace = new ToolboxWorkspaceService();
        ulong epochBefore = _bus.LifecycleEpoch;

        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning));

        Assert.Equal(epochBefore, workspace.CapturedEpoch);
        Assert.Equal(1, _bus.GetSubscriberCount<MatchInitializedEvent>());
        Assert.Equal(1, _bus.GetSubscriberCount<ReplayStartedEvent>());
        Assert.Equal(1, _bus.GetSubscriberCount<ReplayEndedEvent>());
        Assert.Equal(1, _bus.GetSubscriberCount<StateRestoredEvent>());
    }

    [Fact]
    public void Activate_DuplicatePanelKind_IsRejected()
    {
        using var workspace = new ToolboxWorkspaceService();
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring));

        Assert.Throws<InvalidOperationException>(() =>
            workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring)));
    }

    [Fact]
    public void Activate_AfterClose_IsRejected()
    {
        var workspace = new ToolboxWorkspaceService();
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring));
        workspace.Close();

        Assert.Throws<InvalidOperationException>(() =>
            workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.EventBusDebug)));
    }

    [Fact]
    public void LifecycleBoundary_TracesRevalidatesAndRecapturesEpoch()
    {
        using var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        int revalidations = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning)
        {
            OnRevalidate = () => revalidations++
        });
        ulong captured = workspace.CapturedEpoch;

        _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));

        Assert.Equal(1, traces);
        Assert.Equal(1, revalidations);
        Assert.Equal(captured + 1, workspace.CapturedEpoch);
        Assert.Equal(captured + 1, _bus.LifecycleEpoch);
    }

    [Fact]
    public void QueuedLifecycleBoundary_DispatchThroughProcessFrameActsOnNewEpoch()
    {
        using var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        int revalidations = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning)
        {
            OnRevalidate = () => revalidations++
        });
        ulong captured = workspace.CapturedEpoch;

        _bus.Publish(new ReplayStartedEvent(3, 1));
        _bus.ProcessFrame();

        Assert.Equal(1, traces);
        Assert.Equal(1, revalidations);
        Assert.Equal(captured + 1, workspace.CapturedEpoch);
    }

    [Fact]
    public void StateRestoredBoundary_RevalidatesWithoutEpochChange()
    {
        using var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        int revalidations = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning)
        {
            OnRevalidate = () => revalidations++
        });
        ulong captured = workspace.CapturedEpoch;

        _bus.Publish(new StateRestoredEvent(0, captured));
        _bus.ProcessFrame();

        Assert.Equal(1, traces);
        Assert.Equal(1, revalidations);
        Assert.Equal(captured, workspace.CapturedEpoch); // StateRestored is not a lifecycle type
    }

    [Fact]
    public void StaleEpochCallback_IsRejectedDefensivelyWithoutMutatingState()
    {
        // S4.3-AC12: a stale-epoch callback cannot mutate panel state. Every
        // genuine EventBus lifecycle dispatch bumps the epoch before delivering,
        // so the guard branch is defense-in-depth; reflection delivers a callback
        // with an unchanged epoch to prove the guard rejects it.
        using var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        int revalidations = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning)
        {
            OnRevalidate = () => revalidations++
        });
        ulong captured = workspace.CapturedEpoch;

        DeliverStaleLifecycleCallback(workspace, new MatchInitializedEvent("p1", "p2"));

        Assert.Equal(0, traces);
        Assert.Equal(0, revalidations);
        Assert.Equal(captured, workspace.CapturedEpoch);
    }

    [Fact]
    public void SetPanelVisible_SuspendsAndResumesExactlyOnce()
    {
        using var workspace = new ToolboxWorkspaceService();
        int suspends = 0;
        int resumes = 0;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.EventBusDebug)
        {
            OnSuspend = () => suspends++,
            OnResume = () => resumes++
        });

        workspace.SetPanelVisible(ToolboxPanelKind.EventBusDebug, false);
        workspace.SetPanelVisible(ToolboxPanelKind.EventBusDebug, false); // no-op
        workspace.SetPanelVisible(ToolboxPanelKind.EventBusDebug, true);
        workspace.SetPanelVisible(ToolboxPanelKind.EventBusDebug, true);  // no-op

        Assert.Equal(1, suspends);
        Assert.Equal(1, resumes);
    }

    [Fact]
    public void SetPanelVisible_AfterClose_IsRejected()
    {
        var workspace = new ToolboxWorkspaceService();
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.EventBusDebug));
        workspace.Close();

        Assert.Throws<InvalidOperationException>(() =>
            workspace.SetPanelVisible(ToolboxPanelKind.EventBusDebug, false));
    }

    [Fact]
    public void PlaySessionStarted_TracesOnlyWithoutRevalidating()
    {
        using var workspace = new ToolboxWorkspaceService();
        var traces = new List<string>();
        int revalidations = 0;
        workspace.BoundaryTraced += trace => traces.Add(trace);
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring)
        {
            OnRevalidate = () => revalidations++
        });

        workspace.OnPlaySessionStarted();

        Assert.Single(traces);
        Assert.Contains("play session started", traces[0]);
        Assert.Equal(0, revalidations);
    }

    [Fact]
    public void PlaySessionEnded_TracesAndRevalidates()
    {
        using var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        int revalidations = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning)
        {
            OnRevalidate = () => revalidations++
        });

        workspace.OnPlaySessionEnded();

        Assert.Equal(1, traces);
        Assert.Equal(1, revalidations);
    }

    [Fact]
    public void Close_UnsubscribesObserversBeforeReleasingPanels()
    {
        // Gate item 2 (invalidate before cleanup): when a panel's OnShutdown runs,
        // the workspace's EventBus observers are already unsubscribed.
        var workspace = new ToolboxWorkspaceService();
        int subscriberCountAtShutdown = -1;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring)
        {
            OnShutdown = () => subscriberCountAtShutdown = _bus.GetSubscriberCount<MatchInitializedEvent>()
        });

        workspace.Close();

        Assert.Equal(0, subscriberCountAtShutdown);
        Assert.Equal(0, _bus.GetSubscriberCount<MatchInitializedEvent>());
        Assert.Equal(0, _bus.GetSubscriberCount<ReplayStartedEvent>());
        Assert.Equal(0, _bus.GetSubscriberCount<ReplayEndedEvent>());
        Assert.Equal(0, _bus.GetSubscriberCount<StateRestoredEvent>());
    }

    [Fact]
    public void Close_ShutsDownEachPanelExactlyOnce()
    {
        var workspace = new ToolboxWorkspaceService();
        var shutdownCounts = new Dictionary<ToolboxPanelKind, int>();
        foreach (ToolboxPanelKind kind in new[]
                 {
                     ToolboxPanelKind.MoveAuthoring,
                     ToolboxPanelKind.EventBusDebug,
                     ToolboxPanelKind.RuntimeTuning
                 })
        {
            shutdownCounts[kind] = 0;
            workspace.Activate(new ToolboxPanelHandle(kind)
            {
                OnShutdown = () => shutdownCounts[kind]++
            });
        }

        workspace.Close();

        Assert.Equal(1, shutdownCounts[ToolboxPanelKind.MoveAuthoring]);
        Assert.Equal(1, shutdownCounts[ToolboxPanelKind.EventBusDebug]);
        Assert.Equal(1, shutdownCounts[ToolboxPanelKind.RuntimeTuning]);
        Assert.Equal(0, workspace.PanelCount);
    }

    [Fact]
    public void SecondClose_IsRejectedNotIgnored()
    {
        var workspace = new ToolboxWorkspaceService();
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring));
        workspace.Close();

        Assert.Throws<InvalidOperationException>(() => workspace.Close());
    }

    [Fact]
    public void AfterClose_LifecycleDispatchIsInert()
    {
        var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.RuntimeTuning));
        workspace.Close();

        _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
        _bus.Publish(new ReplayStartedEvent(3, 1));
        _bus.Publish(new StateRestoredEvent(0, 0));
        _bus.ProcessFrame();

        Assert.Equal(0, traces);
        Assert.Equal(0, _bus.GetSubscriberCount<MatchInitializedEvent>());
        Assert.Equal(0, _bus.GetSubscriberCount<ReplayStartedEvent>());
        Assert.Equal(0, _bus.GetSubscriberCount<ReplayEndedEvent>());
        Assert.Equal(0, _bus.GetSubscriberCount<StateRestoredEvent>());
    }

    [Fact]
    public void AfterClose_DelayedPlaySessionCallbackIsInert()
    {
        var workspace = new ToolboxWorkspaceService();
        int traces = 0;
        workspace.BoundaryTraced += _ => traces++;
        workspace.Activate(new ToolboxPanelHandle(ToolboxPanelKind.MoveAuthoring));
        workspace.Close();

        workspace.OnPlaySessionStarted();
        workspace.OnPlaySessionEnded();

        Assert.Equal(0, traces);
    }

    private static void DeliverStaleLifecycleCallback(ToolboxWorkspaceService workspace, object evt)
    {
        MethodInfo method = typeof(ToolboxWorkspaceService).GetMethod(
            "OnLifecycleBoundary", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("workspace lifecycle handler not found");
        method.MakeGenericMethod(evt.GetType()).Invoke(workspace, new[] { evt });
    }
}
