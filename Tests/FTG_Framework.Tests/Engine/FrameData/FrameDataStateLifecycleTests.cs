#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Characters;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using Xunit;

using StateMachineImpl = FTG_Framework.Engine.StateMachine.StateMachine;

namespace FTG_Framework.Tests.Engine.FrameData;

public class FrameDataStateLifecycleTests : IDisposable
{
    public FrameDataStateLifecycleTests() => EventBusTestHelper.Drain();
    public void Dispose() => EventBusTestHelper.Drain();

    [Fact]
    public void NaturalCompletion_CascadesToAuthoritativeDisplayedAndComboIdle()
    {
        var store = Store(Move("test", 1, 1, 1));
        var engine = new FrameDataEngine(store);
        var state = Machine(store);
        var combo = new ComboStateTracker(store);
        combo.Initialize(store);
        var phases = new List<MovePhase>();
        void Observe(MoveFrameChangedEvent evt) => phases.Add(evt.Phase);
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(Observe);
        try
        {
            engine.StartMove(1, "test");
            Run(engine, 3);
            EventBus.Instance.ProcessFrame();

            Assert.Equal(new[] { MovePhase.Startup, MovePhase.Active, MovePhase.Recovery, MovePhase.Idle }, phases);
            Assert.Equal(new[] { CharacterState.Idle }, state.GetStack(1));
            Assert.Equal("[P1] Idle", CharacterController.GetAuthoritativeStateLabel(state, 1));
            Assert.False(combo.IsActive(1));
        }
        finally
        {
            EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(Observe);
            combo.Shutdown();
            state.Shutdown();
        }
    }

    [Fact]
    public void Completion_IsIndependent_AndZeroDurationEndsIdle()
    {
        var store = Store(
            Move("p1", 1, 1, 1),
            Move("p2", 1, 1, 2),
            Move("zero", 0, 0, 0));
        var engine = new FrameDataEngine(store);
        var state = Machine(store);
        try
        {
            engine.StartMove(1, "p1");
            engine.StartMove(2, "p2");
            Run(engine, 3);
            Assert.Equal(CharacterState.Idle, state.GetCurrentState(1));
            Assert.Equal(CharacterState.AttackRecovery, state.GetCurrentState(2));
            Run(engine, 1);
            Assert.Equal(CharacterState.Idle, state.GetCurrentState(2));

            engine.StartMove(1, "zero");
            Run(engine, 1);
            Assert.Equal(CharacterState.Idle, state.GetCurrentState(1));
            Assert.Equal(new[] { CharacterState.Idle }, state.GetStack(1));
        }
        finally { state.Shutdown(); }
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(0, 0, 1)]
    public void ZeroLengthPhases_StillFinishWithTerminalIdle(
        int startup, int active, int recovery)
    {
        var store = Store(Move("edge", startup, active, recovery));
        var engine = new FrameDataEngine(store);
        var state = Machine(store);
        var phases = new List<MovePhase>();
        void Observe(MoveFrameChangedEvent evt) => phases.Add(evt.Phase);
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(Observe);
        try
        {
            engine.StartMove(1, "edge");
            Run(engine, Math.Max(1, startup + active + recovery));
            EventBus.Instance.ProcessFrame();

            Assert.Equal(MovePhase.Idle, phases[^1]);
            Assert.Equal(new[] { CharacterState.Idle }, state.GetStack(1));
        }
        finally
        {
            EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(Observe);
            state.Shutdown();
        }
    }

    private static void Run(FrameDataEngine engine, int count)
    {
        for (int i = 0; i < count; i++)
        {
            engine.Update();
            EventBus.Instance.ProcessFrame();
        }
    }

    private static StateMachineImpl Machine(StubDataStore store)
    {
        var state = new StateMachineImpl(store);
        state.Initialize(store);
        state.InitializePlayer(1);
        state.InitializePlayer(2);
        return state;
    }

    private static StubDataStore Store(params MoveDefinition[] moves)
    {
        var store = new StubDataStore();
        foreach (var move in moves) store.SetMove(move);
        return store;
    }

    private static MoveDefinition Move(string id, int startup, int active, int recovery) => new()
    {
        MoveId = id, Startup = startup, Active = active, Recovery = recovery
    };
}
