#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Core;

public sealed class EventBusEpochTests : IDisposable
{
    private sealed class RecordingSpy : IReplayRecorder
    {
        internal readonly List<object> Events = new();
        public void Record<T>(int frame, T evt) => Events.Add(evt!);
        public ReplayFile Save() => new("test", 3, 0, []);
        public int MaxFrameNumber => 0;
        public int EventCount => Events.Count;
        public bool IsRecording { get; set; } = true;
    }
    private readonly EventBus _bus = EventBus.Instance;

    public EventBusEpochTests()
    {
        EventBusTestHelper.Drain();
        _bus.Recorder = null;
        _bus.SetLifecycleEpochForTesting(1);
    }

    public void Dispose()
    {
        _bus.Recorder = null;
        _bus.RewindFrameCounter(_bus.CurrentFrame);
        _bus.SetLifecycleEpochForTesting(1);
        EventBusTestHelper.Drain();
    }

    [Fact]
    public void ImmediateLifecycle_ActivatesBeforeSubscriberAndClearsContextAfterward()
    {
        ulong observed = 0;
        Action<MatchInitializedEvent> handler = _ => observed = _bus.DispatchEpoch;
        _bus.Subscribe(handler);
        try
        {
            _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));

            Assert.Equal(2UL, observed);
            Assert.Equal(2UL, _bus.LifecycleEpoch);
            Assert.Throws<InvalidOperationException>(() => _ = _bus.DispatchEpoch);
        }
        finally { _bus.Unsubscribe(handler); }
    }

    [Fact]
    public void LifecycleActivation_DropsQueuedOldEpochWork()
    {
        int observedInputs = 0;
        Action<InputReceivedEvent> handler = _ => observedInputs++;
        _bus.Subscribe(handler);
        try
        {
            _bus.Publish(new InputReceivedEvent(1, 0, 1, 1));
            _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
            _bus.ProcessFrame();

            Assert.Equal(0, observedInputs);
        }
        finally { _bus.Unsubscribe(handler); }
    }

    [Fact]
    public void LifecycleSubscriberPublication_UsesNewEpochAndNextFrameQueue()
    {
        ulong lifecycleEpoch = 0;
        ulong publishedEpoch = 0;
        Action<MatchInitializedEvent> lifecycle = _ =>
        {
            lifecycleEpoch = _bus.DispatchEpoch;
            _bus.Publish(new ReplayPausedEvent(7));
        };
        Action<ReplayPausedEvent> published = _ => publishedEpoch = _bus.DispatchEpoch;
        _bus.Subscribe(lifecycle);
        _bus.Subscribe(published);
        try
        {
            _bus.Publish(new MatchInitializedEvent("p1", "p2"));
            _bus.ProcessFrame();
            Assert.Equal(0UL, publishedEpoch);
            _bus.ProcessFrame();

            Assert.Equal(2UL, lifecycleEpoch);
            Assert.Equal(2UL, publishedEpoch);
        }
        finally
        {
            _bus.Unsubscribe(lifecycle);
            _bus.Unsubscribe(published);
        }
    }

    [Fact]
    public void LifecycleEpochExhaustion_RejectsBeforeSubscriberObservation()
    {
        int observed = 0;
        Action<ReplayStartedEvent> handler = _ => observed++;
        _bus.Subscribe(handler);
        _bus.SetLifecycleEpochForTesting(ulong.MaxValue);
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                _bus.PublishImmediate(new ReplayStartedEvent(1, 3)));
            Assert.Equal(0, observed);
            Assert.Equal(ulong.MaxValue, _bus.LifecycleEpoch);
        }
        finally { _bus.Unsubscribe(handler); }
    }

    [Fact]
    public void NestedLifecycle_StopsRemainingOuterEpochSubscribers()
    {
        int staleSubscriberCalls = 0;
        Action<MatchInitializedEvent> first = _ =>
            _bus.PublishImmediate(new ReplayStartedEvent(0, 3));
        Action<MatchInitializedEvent> second = _ => staleSubscriberCalls++;
        _bus.Subscribe(first);
        _bus.Subscribe(second);
        try
        {
            _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
            Assert.Equal(0, staleSubscriberCalls);
            Assert.Equal(3UL, _bus.LifecycleEpoch);
        }
        finally
        {
            _bus.Unsubscribe(first);
            _bus.Unsubscribe(second);
        }
    }

    [Fact]
    public void QueuedLifecycleEpochExhaustion_RejectsAtPublishAndPreservesQueuedWork()
    {
        int inputs = 0;
        Action<InputReceivedEvent> handler = _ => inputs++;
        _bus.Subscribe(handler);
        _bus.SetLifecycleEpochForTesting(ulong.MaxValue);
        try
        {
            _bus.Publish(new InputReceivedEvent(1, 0, 1, 1));
            Assert.Throws<InvalidOperationException>(() =>
                _bus.Publish(new ReplayStartedEvent(1, 3)));
            _bus.ProcessFrame();
            Assert.Equal(1, inputs);
        }
        finally { _bus.Unsubscribe(handler); }
    }

    [Fact]
    public void StaleReloadAndQueuedWork_AreDroppedBeforeRecorderObservation()
    {
        var recorder = new RecordingSpy();
        _bus.Recorder = recorder;
        _bus.EnqueueDataReload(new DataReloadedEvent("moves.json"));
        _bus.Publish(new InputReceivedEvent(1, 0, 1, 1));

        _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
        _bus.ProcessFrame();

        Assert.DoesNotContain(recorder.Events, e => e is DataReloadedEvent or InputReceivedEvent);
        Assert.Contains(recorder.Events, e => e is MatchInitializedEvent);
    }
}
