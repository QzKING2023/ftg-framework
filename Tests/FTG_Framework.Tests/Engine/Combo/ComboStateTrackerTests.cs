#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Engine.Combo;
using Xunit;

namespace FTG_Framework.Tests.Engine.Combo;

[Collection(EventBusTestCollection.Name)]
public class ComboStateTrackerTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    public ComboStateTrackerTests()
    {
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    private static (StubDataStore store, ComboStateTracker tracker, Action dispose) CreateTracker()
    {
        var store = new StubDataStore();
        var tracker = new ComboStateTracker(store);
        tracker.Initialize(store);
        return (store, tracker, () => tracker.Shutdown());
    }

    private static void RunWithTracker(Action<StubDataStore, ComboStateTracker> action)
    {
        var (store, tracker, dispose) = CreateTracker();
        try { action(store, tracker); }
        finally { dispose(); }
    }

    // 4.2
    [Fact]
    public void FirstHitConnected_StartsCombo()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();

            Assert.True(tracker.IsActive(1));
            Assert.Equal(1, tracker.GetHitCount(1));
            Assert.Equal("5LP", tracker.GetCurrentMoveId(1));
            Assert.True(tracker.GetComboStartFrame(1) >= 0);
        });
    }

    [Fact]
    public void FirstHitConnected_PublishesComboStartedEvent()
    {
        RunWithTracker((_, _) =>
        {
            var events = EventBusTestHelper.Collect<ComboStartedEvent>(() =>
            {
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.ProcessFrame();
            });

            Assert.Single(events);
            Assert.Equal(1, events[0].PlayerId);
            Assert.Equal("5LP", events[0].MoveId);
            Assert.Equal(1, events[0].HitCount);
        });
    }

    // 4.3
    [Fact]
    public void SecondHitConnected_IncrementsHitCount()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5MP", 4, 60));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(2, tracker.GetHitCount(1));
            Assert.Equal("5MP", tracker.GetCurrentMoveId(1));
        });
    }

    [Fact]
    public void TwoHits_OnlyOneComboStartedEvent()
    {
        RunWithTracker((_, _) =>
        {
            var events = EventBusTestHelper.Collect<ComboStartedEvent>(() =>
            {
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5MP", 4, 60));
                EventBus.Instance.ProcessFrame();
            });

            Assert.Single(events);
        });
    }

    // 4.4
    [Fact]
    public void MoveBlocked_DoesNotIncrementHitCount()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new MoveBlockedEvent(1, 2, "5HP", 2, 30));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(1, tracker.GetHitCount(1));
            Assert.True(tracker.IsActive(1));
        });
    }

    // 4.5
    [Fact]
    public void ComboEndsAfterAdvantageExpires()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 10, 15, MovePhase.Idle));
            EventBus.Instance.ProcessFrame();
            for (int i = 0; i < 3; i++)
                EventBus.Instance.ProcessFrame();

            Assert.False(tracker.IsActive(1));
            Assert.Equal(0, tracker.GetHitCount(1));
        });
    }

    [Fact]
    public void ComboEndsAfterAdvantageExpires_PublishesComboEndedEvent()
    {
        RunWithTracker((_, _) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 10, 15, MovePhase.Idle));
            EventBus.Instance.ProcessFrame();

            var endedEvents = EventBusTestHelper.Collect<ComboEndedEvent>(() =>
            {
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.ProcessFrame();
            });

            Assert.Single(endedEvents);
            Assert.Equal(1, endedEvents[0].TotalHits);
            Assert.Equal("5LP", endedEvents[0].FinalMove);
        });
    }

    // 4.6
    [Fact]
    public void NeutralAdvantage_EndsImmediatelyOnIdle()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 0, 50));
            EventBus.Instance.ProcessFrame();

            var endedEvents = EventBusTestHelper.Collect<ComboEndedEvent>(() =>
            {
                EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 10, 15, MovePhase.Idle));
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.ProcessFrame();
            });

            Assert.Single(endedEvents);
            Assert.False(tracker.IsActive(1));
        });
    }

    // 4.7
    [Fact]
    public void MultiHitSameFrame_BothIncrement()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 50));
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(2, tracker.GetHitCount(1));
        });
    }

    [Fact]
    public void MultiHitSameFrame_SingleComboStartedEvent()
    {
        RunWithTracker((_, _) =>
        {
            var events = EventBusTestHelper.Collect<ComboStartedEvent>(() =>
            {
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 50));
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.ProcessFrame();
            });

            Assert.Single(events);
        });
    }

    // 4.8
    [Fact]
    public void OnComboHitNull_NoException()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            var ex = Record.Exception(() =>
            {
                EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
                EventBus.Instance.ProcessFrame();
            });

            Assert.Null(ex);
            Assert.True(tracker.IsActive(1));
            Assert.Equal(1, tracker.GetHitCount(1));
        });
    }

    // 4.9
    [Fact]
    public void OnComboHitCallback_ReceivesCorrectArgs()
    {
        RunWithTracker((store, tracker) =>
        {
            var calls = new List<(string moveId, int hitCount)>();
            tracker.SetOnComboHit((moveId, hitCount) => calls.Add((moveId, hitCount)));

            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5MP", 4, 60));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(2, calls.Count);
            Assert.Equal(("5LP", 1), calls[0]);
            Assert.Equal(("5MP", 2), calls[1]);
        });
    }

    // 4.10
    [Fact]
    public void ComboEndedEvent_HasCorrectFields()
    {
        RunWithTracker((_, _) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 0, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5MP", 0, 60));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5HP", 0, 80));
            EventBus.Instance.ProcessFrame();

            var endedEvents = EventBusTestHelper.Collect<ComboEndedEvent>(() =>
            {
                EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5HP", 15, 20, MovePhase.Idle));
                EventBus.Instance.ProcessFrame();
                EventBus.Instance.ProcessFrame();
            });

            Assert.Single(endedEvents);
            Assert.Equal(3, endedEvents[0].TotalHits);
            Assert.Equal("5HP", endedEvents[0].FinalMove);
        });
    }

    // 4.11
    [Fact]
    public void QueryApi_NoActiveCombo_ReturnsDefaults()
    {
        RunWithTracker((store, tracker) =>
        {
            Assert.False(tracker.IsActive(1));
            Assert.Equal(0, tracker.GetHitCount(1));
            Assert.Null(tracker.GetCurrentMoveId(1));
            Assert.Equal(-1, tracker.GetComboStartFrame(1));
        });
    }

    // 4.12
    [Fact]
    public void SameFrameLink_OldIdleDoesNotEndCombo()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 10, 15, MovePhase.Idle));
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5MP", 1, 12, MovePhase.Startup));
            EventBus.Instance.ProcessFrame();

            Assert.True(tracker.IsActive(1));
            Assert.Equal(1, tracker.GetHitCount(1));
        });
    }

    [Fact]
    public void SameFrameLink_ZeroAdvantage_DoesNotEndCombo()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 0, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 10, 15, MovePhase.Idle));
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5MP", 1, 12, MovePhase.Startup));
            EventBus.Instance.ProcessFrame();

            Assert.True(tracker.IsActive(1));
            Assert.Equal(1, tracker.GetHitCount(1));
        });
    }

    // 4.13
    [Fact]
    public void Shutdown_CleansUpState()
    {
        var (store, tracker, dispose) = CreateTracker();

        EventBusTestHelper.Drain();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
        EventBus.Instance.ProcessFrame();
        Assert.True(tracker.IsActive(1));

        tracker.Shutdown();
        Assert.False(tracker.IsActive(1));

        tracker.Initialize(store);
        Assert.False(tracker.IsActive(1));
        Assert.Equal(0, tracker.GetHitCount(1));
        Assert.Equal(-1, tracker.GetComboStartFrame(1));

        dispose();
    }

    [Fact]
    public void SetOnComboHit_Null_UnregistersCallback()
    {
        RunWithTracker((store, tracker) =>
        {
            var calls = new List<(string, int)>();
            tracker.SetOnComboHit((m, h) => calls.Add((m, h)));

            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            Assert.Single(calls);

            tracker.SetOnComboHit(null);
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5MP", 4, 60));
            EventBus.Instance.ProcessFrame();
            Assert.Single(calls);
        });
    }

    [Fact]
    public void MultiHitSameFrame_OnComboHitCalledPerHit()
    {
        RunWithTracker((store, tracker) =>
        {
            var calls = new List<(string moveId, int hitCount)>();
            tracker.SetOnComboHit((moveId, hitCount) => calls.Add((moveId, hitCount)));

            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 50));
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(2, calls.Count);
            Assert.Equal(("5LP", 1), calls[0]);
            Assert.Equal(("5LP", 2), calls[1]);
        });
    }

    [Fact]
    public void BlockWithoutCombo_DoesNotLeakState()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new MoveBlockedEvent(1, 2, "5HP", 2, 30));
            EventBus.Instance.ProcessFrame();

            Assert.False(tracker.IsActive(1));
            Assert.Equal(0, tracker.GetHitCount(1));
            Assert.Null(tracker.GetCurrentMoveId(1));
            Assert.Equal(-1, tracker.GetComboStartFrame(1));
        });
    }

    [Fact]
    public void BlockedHitDuringCombo_ComboRemainsActive()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            Assert.True(tracker.IsActive(1));
            Assert.Equal(1, tracker.GetHitCount(1));

            EventBus.Instance.Publish(new MoveBlockedEvent(1, 2, "5MP", 2, 30));
            EventBus.Instance.ProcessFrame();
            Assert.True(tracker.IsActive(1));
            Assert.Equal(1, tracker.GetHitCount(1));
        });
    }

    [Fact]
    public void IndependentTracks_PerPlayer()
    {
        RunWithTracker((store, tracker) =>
        {
            EventBusTestHelper.Drain();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new HitConnectedEvent(2, 1, "5MK", 4, 40));
            EventBus.Instance.ProcessFrame();

            Assert.True(tracker.IsActive(1));
            Assert.True(tracker.IsActive(2));
            Assert.Equal(1, tracker.GetHitCount(1));
            Assert.Equal(1, tracker.GetHitCount(2));
            Assert.Equal("5LP", tracker.GetCurrentMoveId(1));
            Assert.Equal("5MK", tracker.GetCurrentMoveId(2));
        });
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        var (store, tracker, dispose) = CreateTracker();

        EventBusTestHelper.Drain();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 50));
        EventBus.Instance.ProcessFrame();
        Assert.Equal(1, tracker.GetHitCount(1));

        // Second Initialize should be a no-op (guarded by _initialized)
        tracker.Initialize(store);
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5MP", 4, 60));
        EventBus.Instance.ProcessFrame();
        Assert.Equal(2, tracker.GetHitCount(1));

        dispose();
    }
}
