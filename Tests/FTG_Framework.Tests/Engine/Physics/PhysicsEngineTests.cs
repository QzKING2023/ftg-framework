#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Physics;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public sealed class PhysicsEngineTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

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

    [Fact]
    public void Update_HitWithProfile_StartsAndAdvancesOneAbsoluteTrajectoryEvent()
    {
        var profile = new KnockbackProfile
        {
            ProfileId = "launch", Horizontal = 8, Vertical = 3, Gravity = 1.5f, Friction = 0.3f
        };
        var (engine, frames, _) = MakeEngine(profile, stateMachine: new StubStateMachine());
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        KnockbackAppliedEvent? observed = null;
        Action<KnockbackAppliedEvent> handler = e => observed = e;
        EventBus.Instance.Subscribe(handler);
        try
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.NotNull(observed);
            Assert.Equal(5, observed.Value.WorldX);
            Assert.Equal(0, observed.Value.WorldY);
            Assert.Equal(1UL, observed.Value.GenerationId);
            Assert.Equal(KnockbackPhase.Started, observed.Value.Phase);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Update_GenerationCountersAreIndependentPerPlayer()
    {
        var profile = new KnockbackProfile { ProfileId = "launch", Horizontal = 8, Friction = 0.3f };
        var (engine, frames, _) = MakeEngine(profile, stateMachine: new StubStateMachine());
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        var started = new List<KnockbackAppliedEvent>();
        Action<KnockbackAppliedEvent> handler = e =>
        {
            if (e.Phase == KnockbackPhase.Started) started.Add(e);
        };
        EventBus.Instance.Subscribe(handler);
        try
        {
            frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
            engine.Update();
            EventBus.Instance.ProcessFrame();
            frames.P1 = EvaluatedMoveFrame.Idle;
            frames.P2 = new EvaluatedMoveFrame("5A", 2, 1, MovePhase.Active);
            engine.Update();
            EventBus.Instance.ProcessFrame();

            Assert.Contains(started, e => e.PlayerId == 2 && e.GenerationId == 1);
            Assert.Contains(started, e => e.PlayerId == 1 && e.GenerationId == 1);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void Update_GenerationExhaustionRejectsBeforePublishingOrReplacingTrajectory()
    {
        var profile = new KnockbackProfile { ProfileId = "launch", Horizontal = 8, Friction = 0.3f };
        var (engine, frames, _) = MakeEngine(profile, stateMachine: new StubStateMachine());
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        engine.SetGenerationForTesting(2, ulong.MaxValue);
        int published = 0;
        int hits = 0;
        Action<KnockbackAppliedEvent> handler = _ => published++;
        Action<HitConnectedEvent> hitHandler = _ => hits++;
        EventBus.Instance.Subscribe(handler);
        EventBus.Instance.Subscribe(hitHandler);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => engine.Update());
            Assert.Contains("[Physics]", ex.Message);
            EventBus.Instance.ProcessFrame();
            Assert.Equal(0, published);
            Assert.Equal(0, hits);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(handler);
            EventBus.Instance.Unsubscribe(hitHandler);
        }
    }

    [Fact]
    public void Update_LaterGenerationExhaustionRollsBackEarlierSameUpdateLaunch()
    {
        var profile = new KnockbackProfile { ProfileId = "launch", Horizontal = 8, Friction = 0.3f };
        var (engine, frames, _) = MakeEngine(profile, twoHitboxes: true, stateMachine: new StubStateMachine());
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        engine.SetGenerationForTesting(2, ulong.MaxValue - 1);
        var started = new List<KnockbackAppliedEvent>();
        int hits = 0;
        Action<KnockbackAppliedEvent> knockback = e => started.Add(e);
        Action<HitConnectedEvent> hit = _ => hits++;
        EventBus.Instance.Subscribe(knockback);
        EventBus.Instance.Subscribe(hit);
        try
        {
            Assert.Throws<InvalidOperationException>(() => engine.Update());
            EventBus.Instance.ProcessFrame();
            Assert.Empty(started);
            Assert.Equal(0, hits);

            engine.SetGenerationForTesting(2, 0);
            engine.Update();
            EventBus.Instance.ProcessFrame();
            var accepted = Assert.Single(started, e => e.Phase == KnockbackPhase.Started);
            Assert.Equal(2UL, accepted.GenerationId);
            Assert.Equal(2, hits);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(knockback);
            EventBus.Instance.Unsubscribe(hit);
        }
    }

    [Fact]
    public void HotReload_MidTrajectoryKeepsOldProfile_NextHitUsesNewProfile()
    {
        var oldProfile = new KnockbackProfile
        {
            ProfileId = "launch", Horizontal = 8, Vertical = 3, Gravity = 1.5f, Friction = 0.3f
        };
        var (engine, frames, store) = MakeEngine(oldProfile, stateMachine: new StubStateMachine());
        frames.P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active);
        engine.Register(new StubParticipant(1, -5, DirectionValue.Neutral, true));
        engine.Register(new StubParticipant(2, 5, DirectionValue.Neutral, false));
        string directory = Path.Combine(Path.GetTempPath(), $"ftg-physics-reload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string knockbackPath = Path.Combine(directory, "knockback.json");
        string responsePath = Path.Combine(directory, "response.json");
        File.WriteAllText(knockbackPath,
            """{"knockback_profiles":[{"profile_id":"launch","horizontal":12,"vertical":3,"gravity":9,"friction":0.8}]}""");
        var reload = new PhysicsProfileHotReloadService(
            knockbackPath, responsePath, () => ["default"]);
        reload.Initialize(store);
        var applied = new List<KnockbackAppliedEvent>();
        Action<KnockbackAppliedEvent> handler = applied.Add;
        EventBus.Instance.Subscribe(handler);
        try
        {
            int firstContact = EventBus.Instance.CurrentFrame;
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.True(engine.TryGetHitContext(1, 2, "hit-a", firstContact, out var first));
            Assert.Same(oldProfile, first.KnockbackProfile);

            EventBus.Instance.Publish(new DataReloadedEvent(knockbackPath));
            EventBus.Instance.ProcessFrame();
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(1.5f, applied[^1].Gravity);

            frames.P1 = EvaluatedMoveFrame.Idle;
            engine.Update();
            EventBus.Instance.ProcessFrame();
            frames.P1 = new EvaluatedMoveFrame("5A", 2, 1, MovePhase.Active);
            int secondContact = EventBus.Instance.CurrentFrame;
            engine.Update();
            EventBus.Instance.ProcessFrame();

            Assert.True(engine.TryGetHitContext(1, 2, "hit-a", secondContact, out var second));
            Assert.Equal(12, second.KnockbackProfile!.Horizontal);
            Assert.Equal(9, second.KnockbackProfile.Gravity);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(handler);
            reload.Shutdown();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (PhysicsEngine Engine, StubFrames Frames, DataStore Store) MakeEngine(
        KnockbackProfile? profile = null, bool twoHitboxes = false,
        IStateMachine? stateMachine = null)
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
        var engine = new PhysicsEngine(store, frames, stateMachine);
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

    private sealed class StubStateMachine : IStateMachine
    {
        public PhysicsResponseProfile GetEffectivePhysicsProfile(int playerId) => new()
        {
            ProfileId = "effective", KnockbackMultiplier = 1, GravityScale = 1,
            Friction = 0.5f, AirFriction = 0.2f
        };
        public CharacterState GetCurrentState(int playerId) => CharacterState.Hitstun;
        public IReadOnlyList<CharacterState> GetStack(int playerId) => [CharacterState.Hitstun];
        public int GetStackDepth(int playerId) => 1;
        public void InitializePlayer(int playerId) { }
        public void PushState(int playerId, CharacterState state) { }
        public void PopState(int playerId) { }
        public void ReplaceState(int playerId, CharacterState newState) { }
        public void RegisterStateProfile(CharacterState state, string physicsResponseProfileId) { }
        public void Initialize(IDataStore dataStore) { }
        public void Shutdown() { }
    }
}
