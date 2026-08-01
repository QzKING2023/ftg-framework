#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

[Collection(EventBusTestCollection.Name)]
public class ReplayPlayerTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    private static ReplayFile CreateTestFile(List<ReplayEntry> entries, int frameCount)
    {
        return new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, frameCount, entries);
    }

    [Fact]
    public void GetEventsForFrame_EmptyFrame_ReturnsEmpty()
    {
        var player = new ReplayPlayer();
        player.Load(CreateTestFile(new List<ReplayEntry>(), 0));

        var result = player.GetEventsForFrame(5);
        Assert.Empty(result);
    }

    [Fact]
    public void Load_ThreeFrames_VerifiesFrameCountAndGrouping()
    {
        var entries = new List<ReplayEntry>
        {
            CreateEntry(0, nameof(FrameAdvancedEvent), """{"FrameNumber":0}"""),
            CreateEntry(1, nameof(FrameAdvancedEvent), """{"FrameNumber":1}"""),
            CreateEntry(1, nameof(InputReceivedEvent), """{"PlayerId":1,"Frame":1,"InputType":0,"InputValue":3}"""),
            CreateEntry(2, nameof(FrameAdvancedEvent), """{"FrameNumber":2}"""),
        };

        var player = new ReplayPlayer();
        player.Load(CreateTestFile(entries, 2));

        Assert.Equal(2, player.FrameCount);
        Assert.Equal(4, player.TotalEvents);
        Assert.Single(player.GetEventsForFrame(0));
        Assert.Equal(2, player.GetEventsForFrame(1).Count);
        Assert.Single(player.GetEventsForFrame(2));
        Assert.Empty(player.GetEventsForFrame(3));
    }

    [Fact]
    public void ProcessFrameReplay_DispatchesToSubscribers()
    {
        var entries = new List<ReplayEntry>
        {
            CreateEntry(0, nameof(FrameAdvancedEvent), """{"FrameNumber":0}"""),
            CreateEntry(0, nameof(HitConnectedEvent), """{"AttackerId":1,"DefenderId":2,"MoveId":"5LP","HitAdvantage":3,"Damage":30}"""),
        };

        var player = new ReplayPlayer();
        player.Load(CreateTestFile(entries, 0));

        var received = new List<object>();
        void OnHit(HitConnectedEvent e) => received.Add(e);

        EventBus.Instance.Subscribe<HitConnectedEvent>(OnHit);
        try
        {
            int dispatched = player.ProcessFrameReplay(EventBus.Instance, 0);
            Assert.Equal(2, dispatched);
            Assert.Single(received);
            var hit = Assert.IsType<HitConnectedEvent>(received[0]);
            Assert.Equal("5LP", hit.MoveId);
            Assert.Equal(30, hit.Damage);
        }
        finally
        {
            EventBus.Instance.Unsubscribe<HitConnectedEvent>(OnHit);
        }
    }

    [Fact]
    public void ProcessFrameReplay_DispatchesAllEventsInFrame()
    {
        var entries = new List<ReplayEntry>
        {
            CreateEntry(0, nameof(FrameAdvancedEvent), """{"FrameNumber":0}"""),
        };

        var player = new ReplayPlayer();
        player.Load(CreateTestFile(entries, 0));

        int dispatched = player.ProcessFrameReplay(EventBus.Instance, 0);
        Assert.Equal(1, dispatched);
    }

    [Fact]
    public void Load_NullFile_Throws()
    {
        var player = new ReplayPlayer();
        Assert.Throws<ArgumentNullException>(() => player.Load(null!));
    }

    [Fact]
    public void Load_UnknownEventType_RejectsBeforePlaybackMutation()
    {
        var entries = new List<ReplayEntry>
        {
            new ReplayEntry(0, "UnknownEvent", """{"x":1}"""),
        };

        var player = new ReplayPlayer();
        Assert.Throws<ArgumentException>(() => player.Load(CreateTestFile(entries, 0)));
        Assert.Equal(0, player.TotalEvents);
    }

    [Fact]
    public void AuthoritativeApply_SuppressesDerivedPublicationAlreadyInStream()
    {
        var oldSnapshot = new StateStackSnapshot([CharacterState.Idle]);
        var newSnapshot = new StateStackSnapshot([CharacterState.Idle, CharacterState.AttackStartup]);
        int observed = 0;
        Action<MoveStartedEvent> owner = e => EventBus.Instance.Publish(
            new StateChangedEvent(e.PlayerId, CharacterState.Idle, CharacterState.AttackStartup, newSnapshot));
        Action<StateChangedEvent> observer = _ => observed++;
        EventBus.Instance.Subscribe(owner);
        EventBus.Instance.Subscribe(observer);
        try
        {
            var entries = new List<ReplayEntry>
            {
                new(0, nameof(MoveStartedEvent), "{\"PlayerId\":1,\"MoveId\":\"5LP\"}", 3, 0, 2),
                new(0, nameof(StateChangedEvent), System.Text.Json.JsonSerializer.Serialize(
                    new StateChangedEvent(1, CharacterState.Idle, CharacterState.AttackStartup, newSnapshot)), 5, 0, 2)
            };
            var player = new ReplayPlayer();
            player.Load(CreateTestFile(entries, 0));
            Assert.Equal(2, player.ProcessFrameReplay(EventBus.Instance, 0));
            EventBus.Instance.ProcessFrame();
            Assert.Equal(1, observed);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(owner);
            EventBus.Instance.Unsubscribe(observer);
        }
    }

    private static ReplayEntry CreateEntry(int frame, string eventType, string payload)
    {
        return new ReplayEntry(frame, eventType, payload);
    }
}
