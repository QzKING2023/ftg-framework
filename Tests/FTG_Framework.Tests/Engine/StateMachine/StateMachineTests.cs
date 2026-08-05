#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.StateMachine;
using Xunit;

using StateMachineImpl = FTG_Framework.Engine.StateMachine.StateMachine;

namespace FTG_Framework.Tests.Engine.StateMachine;

[Collection(EventBusTestCollection.Name)]
public class StateMachineTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Subscribers | EventBusResidualState.Queues);
    public StateMachineTests()
    {
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
    }

    private static (StubDataStore store, StateMachineImpl sm, Action dispose) CreateMachine()
    {
        var store = new StubDataStore();
        var sm = new StateMachineImpl(store);
        sm.Initialize(store);
        return (store, sm, () => sm.Shutdown());
    }

    private static void RunWithMachine(Action<StubDataStore, StateMachineImpl> action)
    {
        var (store, sm, dispose) = CreateMachine();
        try
        {
            sm.InitializePlayer(1);
            sm.InitializePlayer(2);
            action(store, sm);
        }
        finally { dispose(); }
    }

    [Fact]
    public void StateEvents_ExposeCanonicalImmutableSnapshots()
    {
        RunWithMachine((_, machine) =>
        {
            StateChangedEvent? changed = null;
            StateStackChangedEvent? stackChanged = null;
            Action<StateChangedEvent> onChanged = value => changed = value;
            Action<StateStackChangedEvent> onStackChanged = value => stackChanged = value;
            EventBus.Instance.Subscribe(onChanged);
            EventBus.Instance.Subscribe(onStackChanged);
            try
            {
                machine.PushState(1, CharacterState.JumpStartup);
                EventBus.Instance.ProcessFrame();
            }
            finally
            {
                EventBus.Instance.Unsubscribe(onChanged);
                EventBus.Instance.Unsubscribe(onStackChanged);
            }

            Assert.Equal(CharacterState.Idle, changed!.Value.OldState);
            Assert.Equal(CharacterState.JumpStartup, changed.Value.NewState);
            Assert.Equal([CharacterState.Idle, CharacterState.JumpStartup], changed.Value.NewSnapshot);
            Assert.Equal([CharacterState.Idle], stackChanged!.Value.OldSnapshot);
            Assert.Equal([CharacterState.Idle, CharacterState.JumpStartup], stackChanged.Value.NewSnapshot);
            Assert.Equal(CharacterState.Idle, StateStackSnapshot.Empty.TopOrIdle);
            Assert.False(changed.Value.NewSnapshot is IList<CharacterState>);
        });
    }

    // ── AC 1: Initial State ──

    [Fact]
    public void InitializePlayer_SetsIdleState()
    {
        RunWithMachine((_, sm) =>
        {
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
            Assert.Equal(1, sm.GetStackDepth(1));
            var stack = sm.GetStack(1);
            Assert.Single(stack);
            Assert.Equal(CharacterState.Idle, stack[0]);
        });
    }

    [Fact]
    public void InitializePlayer_Reinitialization_IsRejected()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.Walk);
            Assert.Equal(2, sm.GetStackDepth(1));

            // Second InitializePlayer should not clobber the existing stack
            sm.InitializePlayer(1);
            Assert.Equal(2, sm.GetStackDepth(1));
            Assert.Equal(CharacterState.Walk, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void InitializePlayer_PublishesNoEvents()
    {
        var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
        {
            var store = new StubDataStore();
            var sm = new StateMachineImpl(store);
            sm.Initialize(store);
            sm.InitializePlayer(1);
        });
        Assert.Empty(events);
    }

    // ── AC 2: Push State + Event ──

    [Fact]
    public void PushState_TransitionsCorrectly()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.JumpStartup);

            Assert.Equal(CharacterState.JumpStartup, sm.GetCurrentState(1));
            Assert.Equal(2, sm.GetStackDepth(1));
            var stack = sm.GetStack(1);
            Assert.Equal(2, stack.Count);
            Assert.Equal(CharacterState.Idle, stack[0]);
            Assert.Equal(CharacterState.JumpStartup, stack[1]);
        });
    }

    [Fact]
    public void PushState_PublishesStateChangedEvent()
    {
        RunWithMachine((_, sm) =>
        {
            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.JumpStartup);
            });

            Assert.Single(events);
            var e = events[0];
            Assert.Equal(1, e.PlayerId);
            Assert.Equal(CharacterState.Idle, e.OldState);
            Assert.Equal(CharacterState.JumpStartup, e.NewState);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.JumpStartup }, e.NewSnapshot);
        });
    }

    [Fact]
    public void PushState_PublishesStateStackChangedEvent()
    {
        RunWithMachine((_, sm) =>
        {
            var events = EventBusTestHelper.Collect<StateStackChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.Walk);
            });

            Assert.Single(events);
            Assert.Equal(new[] { CharacterState.Idle }, events[0].OldSnapshot);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.Walk }, events[0].NewSnapshot);
        });
    }

    [Fact]
    public void PushState_Idle_IsRejected()
    {
        RunWithMachine((_, sm) =>
        {
            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.Idle);
            });

            Assert.Empty(events);
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
        });
    }

    // ── AC 3: ReplaceState (atomic) ──

    [Fact]
    public void ReplaceState_AtomicOperation()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.JumpStartup);
            EventBusTestHelper.Drain();

            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.ReplaceState(1, CharacterState.Hitstun);
            });

            Assert.Single(events);
            var e = events[0];
            Assert.Equal(CharacterState.JumpStartup, e.OldState);
            Assert.Equal(CharacterState.Hitstun, e.NewState);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.Hitstun }, e.NewSnapshot);
        });
    }

    [Fact]
    public void ReplaceState_WhenTopIsIdle_PushesWithoutPop()
    {
        RunWithMachine((_, sm) =>
        {
            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.ReplaceState(1, CharacterState.Hitstun);
            });

            Assert.Single(events);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.Hitstun }, events[0].NewSnapshot);
        });
    }

    [Fact]
    public void ReplaceState_PublishesSingleEvent()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.AttackStartup);
            EventBusTestHelper.Drain();

            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.ReplaceState(1, CharacterState.Hitstun);
            });

            Assert.Single(events);
        });
    }

    // ── AC 4: Transition Guards ──

    [Fact]
    public void InvalidTransition_FromHitstun_Rejected()
    {
        RunWithMachine((_, sm) =>
        {
            sm.ReplaceState(1, CharacterState.Hitstun);

            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.Walk);
            });

            Assert.Empty(events);
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void InvalidTransition_FromHitstun_JumpRejected()
    {
        RunWithMachine((_, sm) =>
        {
            sm.ReplaceState(1, CharacterState.Hitstun);

            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.JumpStartup);
            });

            Assert.Empty(events);
        });
    }

    [Fact]
    public void ValidTransition_FromIdle_ToWalk_Allowed()
    {
        RunWithMachine((_, sm) =>
        {
            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.Walk);
            });

            Assert.Single(events);
            Assert.Equal(CharacterState.Walk, sm.GetCurrentState(1));
        });
    }

    // ── AC 5: Multiple Coexisting States ──

    [Fact]
    public void MultipleStates_CoexistOnStack()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.JumpStartup);
            sm.PushState(1, CharacterState.JumpActive);
            sm.PushState(1, CharacterState.Airborne);
            sm.PushState(1, CharacterState.AttackStartup);

            var stack = sm.GetStack(1);
            Assert.Equal(5, stack.Count);
            Assert.Equal(CharacterState.Idle, stack[0]);
            Assert.Equal(CharacterState.JumpStartup, stack[1]);
            Assert.Equal(CharacterState.JumpActive, stack[2]);
            Assert.Equal(CharacterState.Airborne, stack[3]);
            Assert.Equal(CharacterState.AttackStartup, stack[4]);
        });
    }

    [Fact]
    public void TopOfStack_IsCurrentState()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.Airborne);
            sm.PushState(1, CharacterState.AttackStartup);

            Assert.Equal(CharacterState.AttackStartup, sm.GetCurrentState(1));
        });
    }

    // ── AC 6: Pop State ──

    [Fact]
    public void PopState_ReturnsToPrevious()
    {
        RunWithMachine((_, sm) =>
        {
            // Build stack: Idle → JumpStartup → JumpActive → Airborne
            sm.PushState(1, CharacterState.JumpStartup);
            sm.PushState(1, CharacterState.JumpActive);
            sm.PushState(1, CharacterState.Airborne);
            sm.PushState(1, CharacterState.AttackStartup);

            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PopState(1);
            });

            Assert.Single(events);
            Assert.Equal(CharacterState.Airborne, sm.GetCurrentState(1));
            Assert.Equal(CharacterState.AttackStartup, events[0].OldState);
            Assert.Equal(CharacterState.Airborne, events[0].NewState);
            Assert.Equal(4, events[0].NewSnapshot.Count);
        });
    }

    [Fact]
    public void PopState_PublishesBothEvents()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.Walk);

            var (changed, stack) = EventBusTestHelper.Collect<StateChangedEvent, StateStackChangedEvent>(() =>
            {
                sm.PopState(1);
            });

            Assert.Single(changed);
            Assert.Single(stack);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.Walk }, stack[0].OldSnapshot);
            Assert.Equal(new[] { CharacterState.Idle }, stack[0].NewSnapshot);
        });
    }

    // ── AC 7: Pop Idle Guarded ──

    [Fact]
    public void PopState_WhenOnlyIdle_IsRejected()
    {
        RunWithMachine((_, sm) =>
        {
            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PopState(1);
            });

            Assert.Empty(events);
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
            Assert.Equal(1, sm.GetStackDepth(1));
        });
    }

    // ── AC 8: GetEffectivePhysicsProfile ──

    [Fact]
    public void GetEffectivePhysicsProfile_MergesTopDown()
    {
        RunWithMachine((store, sm) =>
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "default",
                KnockbackMultiplier = 1.0f,
                GravityScale = 1.0f,
                Friction = 0.5f,
                AirFriction = 0.2f,
                ParticipatesInHitstop = true
            });

            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.8f,
                GravityScale = 1.0f,
                Friction = 0.3f,
                AirFriction = 0.1f,
                ParticipatesInHitstop = true
            });

            sm.RegisterStateProfile(CharacterState.Idle, "default");
            sm.RegisterStateProfile(CharacterState.Hitstun, "hitstun");

            sm.ReplaceState(1, CharacterState.Hitstun);

            var profile = sm.GetEffectivePhysicsProfile(1);

            Assert.Equal(0.8f, profile.KnockbackMultiplier);
            Assert.Equal(0.3f, profile.Friction);
            Assert.Equal(0.1f, profile.AirFriction);
        });
    }

    [Fact]
    public void GetEffectivePhysicsProfile_TopOverridesBottom()
    {
        RunWithMachine((store, sm) =>
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "idle_profile",
                KnockbackMultiplier = 1.0f,
                Friction = 0.5f
            });

            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "air_profile",
                KnockbackMultiplier = 0.5f,
                Friction = 0.2f
            });

            sm.RegisterStateProfile(CharacterState.Idle, "idle_profile");
            sm.RegisterStateProfile(CharacterState.Airborne, "air_profile");

            // Build stack: Idle → JumpStartup → JumpActive → Airborne
            sm.PushState(1, CharacterState.JumpStartup);
            sm.PushState(1, CharacterState.JumpActive);
            sm.PushState(1, CharacterState.Airborne);

            var profile = sm.GetEffectivePhysicsProfile(1);

            Assert.Equal(0.5f, profile.KnockbackMultiplier);
            Assert.Equal(0.2f, profile.Friction);
        });
    }

    [Fact]
    public void EffectiveProfile_ReloadAlone_DoesNotRefreshCurrentOccupancy()
    {
        var (store, sm, dispose) = CreateMachine();
        try
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.5f
            });
            sm.RegisterStateProfile(CharacterState.Hitstun, "hitstun");
            sm.InitializePlayer(1);
            sm.ReplaceState(1, CharacterState.Hitstun);
            Assert.Equal(0.5f, sm.GetEffectivePhysicsProfile(1).KnockbackMultiplier);

            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.9f
            });

            Assert.Equal(0.5f, sm.GetEffectivePhysicsProfile(1).KnockbackMultiplier);
        }
        finally { dispose(); }
    }

    [Fact]
    public void EffectiveProfile_SameLogicalHitstunReplacement_DoesNotRefresh()
    {
        var (store, sm, dispose) = CreateMachine();
        try
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.5f
            });
            sm.RegisterStateProfile(CharacterState.Hitstun, "hitstun");
            sm.InitializePlayer(1);
            sm.ReplaceState(1, CharacterState.Hitstun);
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.9f
            });

            sm.ReplaceState(1, CharacterState.Hitstun);

            Assert.Equal(0.5f, sm.GetEffectivePhysicsProfile(1).KnockbackMultiplier);
        }
        finally { dispose(); }
    }

    [Fact]
    public void EffectiveProfile_ExitAndReenter_AdoptsReloadedProfile()
    {
        var (store, sm, dispose) = CreateMachine();
        try
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.5f
            });
            sm.RegisterStateProfile(CharacterState.Hitstun, "hitstun");
            sm.InitializePlayer(1);
            sm.ReplaceState(1, CharacterState.Hitstun);
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "hitstun",
                KnockbackMultiplier = 0.9f
            });

            sm.PopState(1);
            sm.ReplaceState(1, CharacterState.Hitstun);

            Assert.Equal(0.9f, sm.GetEffectivePhysicsProfile(1).KnockbackMultiplier);
        }
        finally { dispose(); }
    }

    [Fact]
    public void InitializePlayer_AfterMappingRegistration_CachesMappedIdleProfile()
    {
        var (store, sm, dispose) = CreateMachine();
        try
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "idle",
                KnockbackMultiplier = 0.4f
            });
            sm.RegisterStateProfile(CharacterState.Idle, "idle");

            sm.InitializePlayer(1);
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "idle",
                KnockbackMultiplier = 0.8f
            });

            Assert.Equal(0.4f, sm.GetEffectivePhysicsProfile(1).KnockbackMultiplier);
        }
        finally { dispose(); }
    }

    [Fact]
    public void StateChangedObserver_SeesCommittedEffectiveProfileSnapshot()
    {
        var (store, sm, dispose) = CreateMachine();
        store.SetPhysicsResponseProfile(new PhysicsResponseProfile
        {
            ProfileId = "hitstun",
            KnockbackMultiplier = 0.65f
        });
        sm.RegisterStateProfile(CharacterState.Hitstun, "hitstun");
        sm.InitializePlayer(1);
        float observed = -1;
        Action<StateChangedEvent> handler = e =>
        {
            if (e.PlayerId == 1)
                observed = sm.GetEffectivePhysicsProfile(1).KnockbackMultiplier;
        };
        EventBus.Instance.Subscribe(handler);
        try
        {
            sm.ReplaceState(1, CharacterState.Hitstun);
            EventBus.Instance.ProcessFrame();
            Assert.Equal(0.65f, observed);
        }
        finally
        {
            EventBus.Instance.Unsubscribe(handler);
            dispose();
        }
    }

    // ── AC 9: Custom State + Custom Profile ──

    [Fact]
    public void RegisterStateProfile_AssociatesCorrectly()
    {
        RunWithMachine((store, sm) =>
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "custom_blockstun",
                KnockbackMultiplier = 0.3f
            });

            sm.RegisterStateProfile(CharacterState.Blockstun, "custom_blockstun");
            sm.ReplaceState(1, CharacterState.Blockstun);

            var profile = sm.GetEffectivePhysicsProfile(1);

            Assert.Equal(0.3f, profile.KnockbackMultiplier);
        });
    }

    // ── AC 10: Empty Stack Default ──

    [Fact]
    public void GetEffectivePhysicsProfile_UninitializedPlayer_ReturnsDefault()
    {
        var (store, sm, dispose) = CreateMachine();
        try
        {
            var profile = sm.GetEffectivePhysicsProfile(1);

            Assert.Equal(1.0f, profile.KnockbackMultiplier);
            Assert.Equal(1.0f, profile.GravityScale);
            Assert.Equal(0.5f, profile.Friction);
            Assert.Equal(0.2f, profile.AirFriction);
            Assert.True(profile.ParticipatesInHitstop);
        }
        finally { dispose(); }
    }

    [Fact]
    public void GetEffectivePhysicsProfile_UnregisteredState_UsesDefault()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.Walk);

            var profile = sm.GetEffectivePhysicsProfile(1);

            Assert.Equal(1.0f, profile.KnockbackMultiplier);
        });
    }

    // ── AC 11: Event Payloads ──

    [Fact]
    public void EventPayload_ContainsCanonicalNewSnapshot()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.JumpStartup);
            sm.PushState(1, CharacterState.JumpActive);

            EventBusTestHelper.Drain();

            var events = EventBusTestHelper.Collect<StateChangedEvent>(() =>
            {
                sm.PushState(1, CharacterState.AttackStartup);
            });

            Assert.Single(events);
            Assert.Equal(CharacterState.JumpActive, events[0].OldState);
            Assert.Equal(CharacterState.AttackStartup, events[0].NewState);
            Assert.Equal(4, events[0].NewSnapshot.Count);
            Assert.Equal(CharacterState.Idle, events[0].NewSnapshot[0]);
            Assert.Equal(CharacterState.AttackStartup, events[0].NewSnapshot[^1]);
        });
    }

    // ── Player 2 independence ──

    [Fact]
    public void Player2_IndependentStack()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.Walk);

            Assert.Equal(CharacterState.Walk, sm.GetCurrentState(1));
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(2));
            Assert.Equal(2, sm.GetStackDepth(1));
            Assert.Equal(1, sm.GetStackDepth(2));
        });
    }

    // ── Stack copy safety ──

    [Fact]
    public void GetStack_ReturnsCopy()
    {
        RunWithMachine((_, sm) =>
        {
            var stack = (List<CharacterState>)sm.GetStack(1);
            stack.Add(CharacterState.Walk);

            Assert.Equal(1, sm.GetStackDepth(1));
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void GetStackDepth_MatchesStackCount()
    {
        RunWithMachine((_, sm) =>
        {
            Assert.Equal(1, sm.GetStackDepth(1));
            sm.PushState(1, CharacterState.Walk);
            Assert.Equal(2, sm.GetStackDepth(1));
            sm.PushState(1, CharacterState.JumpStartup);
            Assert.Equal(3, sm.GetStackDepth(1));
        });
    }

    // ── Shutdown ──

    [Fact]
    public void Shutdown_ClearsState()
    {
        var (store, sm, dispose) = CreateMachine();
        sm.InitializePlayer(1);
        sm.PushState(1, CharacterState.Walk);
        dispose();

        Assert.Equal(0, sm.GetStackDepth(1));
    }

    [Fact]
    public void Shutdown_PreventsEventHandlers()
    {
        var (store, sm, dispose) = CreateMachine();
        sm.InitializePlayer(1);
        dispose();

        // After shutdown, event handlers should not fire
        EventBusTestHelper.Drain();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5A", 5, 50));
        EventBus.Instance.ProcessFrame();

        Assert.Equal(0, sm.GetStackDepth(1));
    }

    // ── RegisterStateProfile ──

    [Fact]
    public void RegisterStateProfile_NonexistentProfileId_LogsWarning()
    {
        RunWithMachine((_, sm) =>
        {
            // Should not throw — just logs warning for unknown profile
            sm.RegisterStateProfile(CharacterState.Hitstun, "nonexistent_profile");

            // Querying effective profile with this mapping returns defaults
            sm.ReplaceState(1, CharacterState.Hitstun);
            var profile = sm.GetEffectivePhysicsProfile(1);
            Assert.Equal(1.0f, profile.KnockbackMultiplier);
        });
    }

    // ── Merge direction: top-of-stack wins ──

    [Fact]
    public void GetEffectivePhysicsProfile_TopWins_NotBottom()
    {
        RunWithMachine((store, sm) =>
        {
            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "bottom_hit",
                KnockbackMultiplier = 0.3f
            });

            store.SetPhysicsResponseProfile(new PhysicsResponseProfile
            {
                ProfileId = "top_hit",
                KnockbackMultiplier = 0.8f
            });

            sm.RegisterStateProfile(CharacterState.Idle, "bottom_hit");
            sm.RegisterStateProfile(CharacterState.Hitstun, "top_hit");

            sm.ReplaceState(1, CharacterState.Hitstun);

            var profile = sm.GetEffectivePhysicsProfile(1);

            // Top (Hitstun: 0.8) should win, not bottom (Idle: 0.3)
            Assert.Equal(0.8f, profile.KnockbackMultiplier);
        });
    }

    // ── Event-Driven Transitions ──

    [Fact]
    public void OnMoveStarted_PushesAttackStartup()
    {
        RunWithMachine((_, sm) =>
        {
            EventBusTestHelper.Drain();

            EventBus.Instance.Publish(new MoveStartedEvent(1, "5A"));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.AttackStartup, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void OnMoveFrameChanged_TransitionsPhases()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.AttackStartup);
            EventBusTestHelper.Drain();

            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5A", 3, 10, MovePhase.Active));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.AttackActive, sm.GetCurrentState(1));

            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5A", 8, 10, MovePhase.Recovery));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.AttackRecovery, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void OnHitConnected_DefenderGoesToHitstun()
    {
        RunWithMachine((_, sm) =>
        {
            EventBusTestHelper.Drain();

            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5A", 5, 50));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void OnMoveBlocked_DefenderGoesToBlockstun()
    {
        RunWithMachine((_, sm) =>
        {
            EventBusTestHelper.Drain();

            EventBus.Instance.Publish(new MoveBlockedEvent(1, 2, "5A", 2, 40));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.Blockstun, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void OnMoveFrameChanged_Idle_ResetsToIdle()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.AttackStartup);
            sm.PushState(1, CharacterState.AttackActive);
            sm.PushState(1, CharacterState.AttackRecovery);
            EventBusTestHelper.Drain();

            // Simulate move ending — FrameDataEngine sends Idle phase
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, string.Empty, 0, 0, MovePhase.Idle));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
            Assert.Equal(1, sm.GetStackDepth(1));
        });
    }

    [Fact]
    public void OnMoveStarted_BypassesGuard_WhenGuardWouldReject()
    {
        RunWithMachine((_, sm) =>
        {
            // Put player in Hitstun — guard table says Hitstun → AttackStartup is NOT allowed
            sm.ReplaceState(1, CharacterState.Hitstun);
            EventBusTestHelper.Drain();

            // MoveStarted is authoritative — should bypass guard and push AttackStartup
            EventBus.Instance.Publish(new MoveStartedEvent(1, "5A"));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.AttackStartup, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void OnMoveCanceled_PopsAttackStates()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.AttackStartup);
            sm.PushState(1, CharacterState.AttackActive);
            EventBusTestHelper.Drain();

            EventBus.Instance.Publish(new MoveCanceledEvent(1, "5B", "5C", "special"));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
        });
    }

    [Fact]
    public void OnComboEnded_ResetsToIdle()
    {
        RunWithMachine((_, sm) =>
        {
            sm.ReplaceState(1, CharacterState.Hitstun);
            EventBusTestHelper.Drain();

            EventBus.Instance.Publish(new ComboEndedEvent(1, 3, "5B"));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(1));
        });
    }

    // ── PlayerId Validation ──

    [Fact]
    public void InvalidPlayerId_Throws()
    {
        RunWithMachine((_, sm) =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.PushState(0, CharacterState.Walk));
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.PushState(3, CharacterState.Walk));
        });
    }

    [Fact]
    public void InvalidPlayerId_QueryMethods_Throws()
    {
        RunWithMachine((_, sm) =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.GetCurrentState(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.GetCurrentState(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.GetStack(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.GetStackDepth(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => sm.GetEffectivePhysicsProfile(-1));
        });
    }

    // ── StateStackChangedEvent on all mutation operations ──

    [Fact]
    public void ReplaceState_PublishesStateStackChangedEvent()
    {
        RunWithMachine((_, sm) =>
        {
            sm.PushState(1, CharacterState.JumpStartup);
            EventBusTestHelper.Drain();

            var events = EventBusTestHelper.Collect<StateStackChangedEvent>(() =>
            {
                sm.ReplaceState(1, CharacterState.Hitstun);
            });

            Assert.Single(events);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.JumpStartup }, events[0].OldSnapshot);
            Assert.Equal(new[] { CharacterState.Idle, CharacterState.Hitstun }, events[0].NewSnapshot);
        });
    }

    [Fact]
    public void KnockbackCompletion_ResetsOnlyLatestHitstunGeneration()
    {
        RunWithMachine((_, sm) =>
        {
            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "hit", 1, 1, 1));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 1, 1, 1, KnockbackPhase.Started));
            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "hit2", 1, 1, 2));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 2, 2, 2, KnockbackPhase.Started));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 1, 1, 3, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));

            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 2, 2, 4, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(2));

            sm.ReplaceState(2, CharacterState.Hitstun);
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 2, 1, 4, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
        });
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-42)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    public void KnockbackEvent_InvalidPlayerBoundary_IsRejectedWithoutMutation(int playerId)
    {
        RunWithMachine((_, sm) =>
        {
            var beforeP1 = sm.GetStack(1).ToArray();
            var beforeP2 = sm.GetStack(2).ToArray();

            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                playerId, 1, 0, 1, 0.1f, 10, 0, 1, 1, 1, KnockbackPhase.Started));

            Assert.Equal(beforeP1, sm.GetStack(1));
            Assert.Equal(beforeP2, sm.GetStack(2));
        });
    }

    [Fact]
    public void KnockbackTuple_RejectsOutOfOrderConflictsAndPostTerminalEvents()
    {
        RunWithMachine((_, sm) =>
        {
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 7, 4, 10, KnockbackPhase.Progressed));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 7, 4, 11, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(2));

            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "hit", 1, 1, 4));
            var started = new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 7, 4, 12, KnockbackPhase.Started);
            EventBus.Instance.PublishImmediate(started);
            EventBus.Instance.PublishImmediate(started); // exact last duplicate
            var progress = started with { FrameNumber = 13, Phase = KnockbackPhase.Progressed };
            EventBus.Instance.PublishImmediate(progress);
            EventBus.Instance.PublishImmediate(progress); // exact last duplicate
            EventBus.Instance.PublishImmediate(started); // old duplicate after progress
            EventBus.Instance.PublishImmediate(progress with { HorizontalForce = 99 }); // same-frame conflict
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));

            var completed = progress with { FrameNumber = 14, Phase = KnockbackPhase.Completed };
            EventBus.Instance.PublishImmediate(completed);
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(2));
            EventBus.Instance.PublishImmediate(completed with
                { FrameNumber = 15, Phase = KnockbackPhase.Progressed });
            Assert.Equal(CharacterState.Idle, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void ReplayBoundaries_ResetTrajectoryGenerationNamespace()
    {
        RunWithMachine((_, sm) =>
        {
            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "hit", 1, 1, 1));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 100, 1, 100, KnockbackPhase.Started));
            EventBus.Instance.PublishImmediate(new ReplayStartedEvent(10, 2));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 1, 1, 1, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));

            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "hit", 1, 1, 2));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 1, 2, 100, KnockbackPhase.Started));
            EventBus.Instance.PublishImmediate(new ReplayEndedEvent(10));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 1, 2, 101, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void LegacyKnockbackCompletion_DoesNotChangeHitstun()
    {
        RunWithMachine((_, sm) =>
        {
            sm.ReplaceState(2, CharacterState.Hitstun);
            EventBus.Instance.Publish(new KnockbackAppliedEvent(2, 0, 0, 1, 0.1f));
            EventBus.Instance.ProcessFrame();
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void QueuedKnockback_OldCompletionCannotClearNewGeneration()
    {
        RunWithMachine((_, sm) =>
        {
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "g1", 1, 1, 10));
            EventBus.Instance.Publish(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 1, 10, 10, KnockbackPhase.Started));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "g2", 1, 1, 11));
            EventBus.Instance.Publish(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 2, 11, 11, KnockbackPhase.Started));
            EventBus.Instance.ProcessFrame();
            EventBus.Instance.Publish(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 1, 10, 12, KnockbackPhase.Completed));
            EventBus.Instance.ProcessFrame();
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void DisplacedHitstun_CannotBeClearedByFormerOwner()
    {
        RunWithMachine((_, sm) =>
        {
            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "g1", 1, 1, 10));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 1, 10, 10, KnockbackPhase.Started));
            sm.ReplaceState(2, CharacterState.Blockstun);
            sm.ReplaceState(2, CharacterState.Hitstun);
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 1, 10, 11, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void StartedGeneration_CannotMoveBackwardWithinEpoch()
    {
        RunWithMachine((_, sm) =>
        {
            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "g2", 1, 1, 20));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 2, 20, 20, KnockbackPhase.Started));
            EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "g1", 1, 1, 21));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 1, 0, 1, 0.1f, 10, 0, 1, 21, 21, KnockbackPhase.Started));
            EventBus.Instance.PublishImmediate(new KnockbackAppliedEvent(
                2, 0, 0, 1, 0.1f, 10, 0, 1, 21, 22, KnockbackPhase.Completed));
            Assert.Equal(CharacterState.Hitstun, sm.GetCurrentState(2));
        });
    }

    [Fact]
    public void PrepareRuntimeSnapshot_RebindsTupleEpochAndFrameNumberToCaptureDomain()
    {
        RunWithMachine((_, sm) =>
        {
            var captured = new StateMachineRuntimeSnapshot(
                new Dictionary<int, List<CharacterState>>
                {
                    [2] = new() { CharacterState.Idle, CharacterState.Hitstun }
                },
                new Dictionary<int, Data.PhysicsResponseProfile>(),
                new Dictionary<int, GenerationEpochSnapshot>(),
                new Dictionary<int, ReactionRecoverySnapshot>(),
                new Dictionary<int, KnockbackTupleRuntimeSnapshot>
                {
                    [2] = new KnockbackTupleRuntimeSnapshot(1, 5,
                        new KnockbackAppliedEvent(2, 0, 0, 0, 0, 0, 0, 5, 0, 800, KnockbackPhase.Progressed),
                        Terminal: false)
                },
                new Dictionary<int, KnockbackOccupancyRuntimeSnapshot> { [2] = new(1, 5) });

            var prepared = sm.PrepareRuntimeSnapshot(
                captured, new SnapshotPrepareContext(1, 9, 40, SnapshotRestoreMode.Normal));

            Assert.Equal(9UL, prepared.KnockbackTuples![2].Epoch);
            Assert.Equal(40, prepared.KnockbackTuples[2].Last.FrameNumber);
            Assert.Equal(9UL, prepared.HitstunOccupancy![2].Epoch);
        });
    }

    [Fact]
    public void PrepareRuntimeSnapshot_NonTerminalTupleWithoutOccupancy_Rejects()
    {
        RunWithMachine((_, sm) =>
        {
            var captured = new StateMachineRuntimeSnapshot(
                new Dictionary<int, List<CharacterState>>
                {
                    [2] = new() { CharacterState.Idle, CharacterState.Hitstun }
                },
                new Dictionary<int, Data.PhysicsResponseProfile>(),
                new Dictionary<int, GenerationEpochSnapshot>(),
                new Dictionary<int, ReactionRecoverySnapshot>(),
                new Dictionary<int, KnockbackTupleRuntimeSnapshot>
                {
                    [2] = new KnockbackTupleRuntimeSnapshot(1, 5,
                        new KnockbackAppliedEvent(2, 0, 0, 0, 0, 0, 0, 5, 0, 4, KnockbackPhase.Progressed),
                        Terminal: false)
                },
                new Dictionary<int, KnockbackOccupancyRuntimeSnapshot>());

            Assert.Throws<SnapshotPrepareException>(() => sm.PrepareRuntimeSnapshot(
                captured, new SnapshotPrepareContext(1, 9, 40, SnapshotRestoreMode.Normal)));
        });
    }

    [Fact]
    public void PrepareRuntimeSnapshot_OccupancyWithoutTuple_Rejects()
    {
        RunWithMachine((_, sm) =>
        {
            var captured = new StateMachineRuntimeSnapshot(
                new Dictionary<int, List<CharacterState>>
                {
                    [2] = new() { CharacterState.Idle, CharacterState.Hitstun }
                },
                new Dictionary<int, Data.PhysicsResponseProfile>(),
                new Dictionary<int, GenerationEpochSnapshot>(),
                new Dictionary<int, ReactionRecoverySnapshot>(),
                new Dictionary<int, KnockbackTupleRuntimeSnapshot>(),
                new Dictionary<int, KnockbackOccupancyRuntimeSnapshot> { [2] = new(1, 5) });

            Assert.Throws<SnapshotPrepareException>(() => sm.PrepareRuntimeSnapshot(
                captured, new SnapshotPrepareContext(1, 9, 40, SnapshotRestoreMode.Normal)));
        });
    }
}
