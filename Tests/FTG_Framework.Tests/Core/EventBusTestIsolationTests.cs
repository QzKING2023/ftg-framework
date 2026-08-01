#nullable enable
using System;
using System.Threading;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Core;

[Collection(EventBusTestCollection.Name)]
public sealed class EventBusTestIsolationTests
{
    [Fact]
    public void Scope_RestoresEveryStateCategory_AndAdvancesGeneration()
    {
        ulong firstGeneration;
        using (var scope = new EventBusTestScope(EventBusResidualState.All))
        {
            firstGeneration = scope.Baseline.TestGeneration;
            DirtyEveryCategory();
        }

        using var second = new EventBusTestScope();
        EventBusTestDiagnostic baseline = second.Baseline;

        Assert.True(baseline.TestGeneration > firstGeneration);
        Assert.Equal(0, baseline.SubscriberTypeCount);
        Assert.Equal(0, baseline.SubscriberCount);
        Assert.Equal(0, baseline.CurrentQueueCount);
        Assert.Equal(0, baseline.NextQueueCount);
        Assert.Equal(0, baseline.PendingReloadCount);
        Assert.Equal(0, baseline.FrameNumber);
        Assert.Equal(0, baseline.DispatchFrame);
        Assert.False(baseline.IsDispatching);
        Assert.Null(baseline.DispatchEpoch);
        Assert.False(baseline.Paused);
        Assert.False(baseline.StepRequested);
        Assert.False(baseline.SuppressFrameAdvanced);
        Assert.False(baseline.HasRecorder);
    }

    [Fact]
    public void Scope_Dispose_IsIdempotent()
    {
        var scope = new EventBusTestScope();
        scope.Dispose();
        scope.Dispose();
    }

    [Fact]
    public void Scope_WrongThreadDisposeFails_AndOwnerCanRetry()
    {
        var scope = new EventBusTestScope();

        Exception? thrown = null;
        var foreignThread = new Thread(() =>
        {
            try
            {
                scope.Dispose();
            }
            catch (Exception error)
            {
                thrown = error;
            }
        });
        foreignThread.Start();
        foreignThread.Join();

        var error = Assert.IsType<InvalidOperationException>(thrown);
        Assert.Contains("owning test thread", error.Message);
        scope.Dispose();
    }

    [Fact]
    public void Scope_RestoresAfterThrownAction()
    {
        void ThrowingAction()
        {
            using var scope = new EventBusTestScope(EventBusResidualState.All);
            DirtyEveryCategory();
            throw new InvalidOperationException("expected");
        }

        Assert.Throws<InvalidOperationException>((Action)ThrowingAction);

        using var clean = new EventBusTestScope();
        Assert.True(clean.Baseline.IsClean);
    }

    [Fact]
    public void Diagnostic_IdentifiesEveryResidualCategory()
    {
        using var scope = new EventBusTestScope(EventBusResidualState.All);
        DirtyEveryCategory();
        EventBusTestDiagnostic dirty = EventBus.Instance.GetTestDiagnostic();

        Assert.False(dirty.IsClean);
        Assert.True(dirty.SubscriberTypeCount > 0);
        Assert.True(dirty.SubscriberCount > 0);
        Assert.True(dirty.CurrentQueueCount > 0);
        Assert.True(dirty.PendingReloadCount > 0);
        Assert.True(dirty.Paused);
        Assert.True(dirty.StepRequested);
        Assert.True(dirty.SuppressFrameAdvanced);
        Assert.True(dirty.HasRecorder);
    }

    private static void DirtyEveryCategory()
    {
        EventBus bus = EventBus.Instance;
        bus.Subscribe<FrameAdvancedEvent>(_ => { });
        bus.Publish(new FrameAdvancedEvent(99));
        bus.EnqueueDataReload(new DataReloadedEvent("dirty.json"));
        bus.Paused = true;
        bus.StepRequested = true;
        bus.SuppressFrameAdvanced = true;
        bus.Recorder = new StubRecorder();
    }

    private sealed class StubRecorder : IReplayRecorder
    {
        public bool IsRecording { get; set; } = true;
        public int MaxFrameNumber => 0;
        public int EventCount => 0;
        public void Record<T>(int frame, T evt) { }
        public ReplayFile Save() => throw new NotSupportedException();
    }
}
