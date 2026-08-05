#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Core;

[Collection(EventBusTestCollection.Name)]
public sealed class EventBusEpochTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Recorder);
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
    private readonly ulong _initialEpoch;

    public EventBusEpochTests()
    {
        _initialEpoch = _bus.LifecycleEpoch;
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
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

            Assert.Equal(_initialEpoch + 1, observed);
            Assert.Equal(_initialEpoch + 1, _bus.LifecycleEpoch);
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

            Assert.Equal(_initialEpoch + 1, lifecycleEpoch);
            Assert.Equal(_initialEpoch + 1, publishedEpoch);
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
            Assert.Equal(_initialEpoch + 2, _bus.LifecycleEpoch);
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
    public void StaleEpochSubscriber_MustRevalidateAtDispatchToRejectNewEpochEvent()
    {
        // Adversarial timing scenario (Class A): a subscriber that captured the
        // lifecycle epoch at subscription time, then fails to re-validate at
        // dispatch time, WILL observe a new-epoch event. Envelope-level epoch
        // filtering protects stale *envelopes*; subscriber-level protection
        // requires the capture-at-subscription / revalidate-at-dispatch pattern.
        ulong capturedAtSubscription = _bus.LifecycleEpoch;
        int naiveCalls = 0;
        int guardedCalls = 0;
        Action<InputReceivedEvent> naive = _ => naiveCalls++;
        Action<InputReceivedEvent> guarded = _ =>
        {
            if (_bus.LifecycleEpoch != capturedAtSubscription) return;
            guardedCalls++;
        };
        _bus.Subscribe(naive);
        _bus.Subscribe(guarded);
        try
        {
            _bus.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
            _bus.Publish(new InputReceivedEvent(1, 0, 1, 1));
            _bus.ProcessFrame();

            Assert.Equal(capturedAtSubscription + 1, _bus.LifecycleEpoch);
            Assert.Equal(1, naiveCalls);
            Assert.Equal(0, guardedCalls);
        }
        finally
        {
            _bus.Unsubscribe(naive);
            _bus.Unsubscribe(guarded);
        }
    }

    [Fact]
    public void ProcessFrame_AtInt32MaxFrame_ThrowsInsteadOfWrapping()
    {
        _bus.RewindFrameCounter(int.MaxValue - 1);
        _bus.ProcessFrame();
        Assert.Equal(int.MaxValue, _bus.CurrentFrame);

        Assert.Throws<InvalidOperationException>(() => _bus.ProcessFrame());
        Assert.Equal(int.MaxValue, _bus.CurrentFrame);
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
