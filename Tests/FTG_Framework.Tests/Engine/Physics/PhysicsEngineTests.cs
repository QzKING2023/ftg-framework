#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Physics;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class PhysicsEngineTests : IDisposable
{
    public PhysicsEngineTests() => EventBusTestHelper.Drain();

    public void Dispose() => EventBusTestHelper.Drain();

    [Fact]
    public void Update_ActiveOverlap_PublishesSameContactFrameAndStoresSnapshot()
    {
        var profile = new KnockbackProfile { ProfileId = "light", Horizontal = 8 };
        var (engine, frames, store) = MakeEngine(profile);
        frames.P1 = new EvaluatedMoveFrame("5A", 7, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        HitConnectedEvent? observed = null;
        Action<HitConnectedEvent> handler = e => observed = e;
        EventBus.Instance.Subscribe(handler);
        try
        {
            int expectedFrame = EventBus.Instance.CurrentFrame;
            engine.Update();
            EventBus.Instance.ProcessFrame();

            Assert.NotNull(observed);
            Assert.Equal(expectedFrame, observed!.Value.ContactFrame);
            Assert.True(engine.TryGetHitContext(1, 2, "hit-a", expectedFrame, out var context));
            Assert.Same(profile, context.KnockbackProfile);
            store.SetKnockbackProfiles(
                [new KnockbackProfile { ProfileId = "light", Horizontal = 99 }]);
            Assert.Equal(8, context.KnockbackProfile!.Horizontal);
            Assert.NotSame(store.GetKnockbackProfile("light"), context.KnockbackProfile);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Update_Blocking_PublishesBlockOnly()
    {
        var (engine, frames, _) = MakeEngine();
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Forward, false));
        int hits = 0, blocks = 0;
        Action<HitConnectedEvent> hit = _ => hits++;
        Action<MoveBlockedEvent> block = _ => blocks++;
        EventBus.Instance.Subscribe(hit);
        EventBus.Instance.Subscribe(block);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(0, hits);
            Assert.Equal(1, blocks);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(hit);
            EventBus.Instance.Unsubscribe(block);
        }
    }

    [Fact]
    public void Update_TwoHitboxes_PublishesTwice_ButContinuousOverlapDoesNotRehit()
    {
        var (engine, frames, _) = MakeEngine(twoHitboxes: true);
        frames.P1 = new EvaluatedMoveFrame("5A", 3, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        int hits = 0;
        Action<HitConnectedEvent> handler = _ => hits++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(2, hits);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Update_Startup_DoesNotPublish()
    {
        var (engine, frames, _) = MakeEngine();
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 0, MovePhase.Startup);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        int hits = 0;
        Action<HitConnectedEvent> handler = _ => hits++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(0, hits);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Update_DefenderMoveFrameHurtbox_ReplacesNeutralHurtbox()
    {
        var attack = new MoveDefinition
        {
            MoveId = "attack", Active = 1,
            CollisionFrames = new[]
            {
                new CollisionFrameDefinition
                {
                    Frame = 1,
                    Hitboxes = new[]
                    {
                        new CollisionBoxDefinition
                            { BoxId = "hit", X = 10, Width = 20, Height = 20 }
                    }
                }
            }
        };
        var defend = new MoveDefinition
        {
            MoveId = "defend", Active = 1,
            CollisionFrames = new[]
            {
                new CollisionFrameDefinition
                {
                    Frame = 1,
                    Hurtboxes = new[]
                    {
                        new CollisionBoxDefinition
                            { BoxId = "lean", X = 100, Width = 20, Height = 20 }
                    }
                }
            }
        };
        var store = new DataStore(new[] { attack, defend });
        var frames = new StubFrames
        {
            P1 = new EvaluatedMoveFrame("attack", 1, 0, MovePhase.Active),
            P2 = new EvaluatedMoveFrame("defend", 1, 0, MovePhase.Active)
        };
        var engine = new PhysicsEngine(store, frames);
        engine.Initialize(store);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        int hits = 0;
        Action<HitConnectedEvent> handler = _ => hits++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(0, hits);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Update_DefenderMoveFrameWithNoHurtboxes_DoesNotUseNeutralHurtbox()
    {
        var attack = new MoveDefinition
        {
            MoveId = "attack", Active = 1,
            CollisionFrames =
            [
                new CollisionFrameDefinition
                {
                    Frame = 1,
                    Hitboxes =
                    [
                        new CollisionBoxDefinition
                            { BoxId = "hit", X = 10, Width = 20, Height = 20 }
                    ]
                }
            ]
        };
        var defend = new MoveDefinition
        {
            MoveId = "defend", Active = 1,
            CollisionFrames = [new CollisionFrameDefinition { Frame = 1 }]
        };
        var store = new DataStore([attack, defend]);
        var frames = new StubFrames
        {
            P1 = new EvaluatedMoveFrame("attack", 1, 0, MovePhase.Active),
            P2 = new EvaluatedMoveFrame("defend", 1, 0, MovePhase.Active)
        };
        var engine = new PhysicsEngine(store, frames);
        engine.Initialize(store);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        int hits = 0;
        Action<HitConnectedEvent> handler = _ => hits++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(0, hits);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Unregister_OldParticipant_DoesNotRemoveReplacement()
    {
        var (engine, frames, _) = MakeEngine();
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        var oldP1 = new StubParticipant(1, -100, DirectionValue.Neutral, true);
        var replacementP1 = new StubParticipant(1, -5, DirectionValue.Neutral, true);
        engine.Register(oldP1);
        engine.Register(replacementP1);
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        engine.Unregister(oldP1);
        int hits = 0;
        Action<HitConnectedEvent> handler = _ => hits++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(1, hits);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Register_SnapshotPlayerIdMismatch_FailsFast()
    {
        var (engine, _, _) = MakeEngine();
        var ex = Assert.Throws<InvalidOperationException>(
            () => engine.Register(new MismatchedParticipant()));
        Assert.Contains("[Physics]", ex.Message);
    }

    [Fact]
    public void Update_TwoHitboxes_DispatchesHitboxIdsInLifoOrder()
    {
        var (engine, frames, _) = MakeEngine(twoHitboxes: true);
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        var observed = new List<string?>();
        Action<HitConnectedEvent> handler = e => observed.Add(e.HitboxId);
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(new[] { "hit-b", "hit-a" }, observed);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Theory]
    [InlineData(DirectionValue.Forward, -5, 5, true)]
    [InlineData(DirectionValue.Back, -5, 5, false)]
    [InlineData(DirectionValue.Back, 5, -5, true)]
    [InlineData(DirectionValue.Forward, 5, -5, false)]
    [InlineData(DirectionValue.Back, 0, 0, false)]
    public void Blocking_IsWorldRelativeAndSymmetric(
        DirectionValue direction, float attackerX, float defenderX, bool expected) =>
        Assert.Equal(expected, PhysicsEngine.IsBlocking(direction, attackerX, defenderX));

    [Fact]
    public void Update_TwoHitboxes_ComboTrackerCountsTwo()
    {
        var (engine, frames, store) = MakeEngine(twoHitboxes: true);
        frames.P1 = new EvaluatedMoveFrame("5A", 5, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        var tracker = new FTG_Framework.Engine.Combo.ComboStateTracker(store);
        tracker.Initialize(store);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(2, tracker.GetHitCount(1));
        }
        finally { tracker.Shutdown(); }
    }

    private static (PhysicsEngine Engine, StubFrames Frames, DataStore Store) MakeEngine(
        KnockbackProfile? profile = null, bool twoHitboxes = false)
    {
        var boxes = new List<CollisionBoxDefinition>
        {
            new() { BoxId = "hit-a", X = 10, Width = 20, Height = 20 }
        };
        if (twoHitboxes)
            boxes.Add(new CollisionBoxDefinition { BoxId = "hit-b", X = 10, Width = 20, Height = 20 });
        var move = new MoveDefinition
        {
            MoveId = "5A", Startup = 1, Active = 1, Recovery = 1,
            HitAdvantage = 3, BlockAdvantage = -2, Damage = 10,
            KnockbackProfileId = profile?.ProfileId,
            CollisionFrames = new[]
            {
                new CollisionFrameDefinition { Frame = 2, Hitboxes = boxes }
            }
        };
        var store = new DataStore(
            new[] { move }, knockbackProfiles: profile is null ? null : new[] { profile });
        var frames = new StubFrames();
        var engine = new PhysicsEngine(store, frames);
        engine.Initialize(store);
        return (engine, frames, store);
    }

    private sealed class StubFrames : IFrameDataEngine
    {
        internal EvaluatedMoveFrame P1 = EvaluatedMoveFrame.Idle;
        internal EvaluatedMoveFrame P2 = EvaluatedMoveFrame.Idle;
        public EvaluatedMoveFrame GetLastEvaluatedFrame(int playerId) => playerId == 1 ? P1 : P2;
        public void StartMove(int playerId, string moveId) { }
        public void Update() { }
        public MovePhase GetPhase(int playerId) => GetLastEvaluatedFrame(playerId).Phase;
        public int GetCurrentFrame(int playerId) => GetLastEvaluatedFrame(playerId).TimelineFrame;
        public string? GetCurrentMoveId(int playerId) => GetLastEvaluatedFrame(playerId).MoveId;
        public void InterruptAndStart(int playerId, string moveId) { }
        public bool RestoreFrame(int frameNumber) => false;
        public int EarliestSnapshotFrame => -1;
        public FrameStateSnapshot? TryGetSnapshot(int frameNumber) => null;
        public void RestoreFromReplaySnapshot(FrameStateSnapshot snapshot) { }
    }

    private sealed class StubParticipant : IPhysicsParticipant
    {
        private readonly PhysicsParticipantSnapshot _snapshot;
        public StubParticipant(int id, float x, DirectionValue direction, bool facingRight)
        {
            _snapshot = new PhysicsParticipantSnapshot(
                id, $"p{id}", x, 0, direction, facingRight,
                new[] { new CollisionBoxDefinition { BoxId = "body", Width = 20, Height = 40 } });
        }
        public int PlayerId => _snapshot.PlayerId;
        public PhysicsParticipantSnapshot CapturePhysicsSnapshot() => _snapshot;
    }

    private sealed class MismatchedParticipant : IPhysicsParticipant
    {
        public int PlayerId => 1;
        public PhysicsParticipantSnapshot CapturePhysicsSnapshot() =>
            new(2, "wrong", 0, 0, DirectionValue.Neutral, true, []);
    }
}
