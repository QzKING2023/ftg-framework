#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests;

public class FrameDataEngineTests : IDisposable
{
    private static MoveDefinition MakeMove(
        string moveId, int startup, int active, int recovery,
        int hitAdvantage = 0, int blockAdvantage = 0, int damage = 0) => new()
    {
        MoveId = moveId,
        Startup = startup,
        Active = active,
        Recovery = recovery,
        HitAdvantage = hitAdvantage,
        BlockAdvantage = blockAdvantage,
        Damage = damage
    };

    private static (FrameDataEngine engine, StubDataStore store) CreateEngine(
        params MoveDefinition[] moves)
    {
        var store = new StubDataStore();
        foreach (var move in moves)
            store.SetMove(move);
        return (new FrameDataEngine(store), store);
    }

    private static void StepFrame()
    {
        EventBus.Instance.ProcessFrame();
    }

    public void Dispose()
    {
        EventBusTestHelper.Drain();
    }

    // --- Phase transitions (AC 1) ---

    [Fact]
    public void PhaseTransitions_OccurAtExactFrames()
    {
        var (engine, _) = CreateEngine(MakeMove("test", startup: 5, active: 3, recovery: 7));

        engine.StartMove(1, "test");
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
        Assert.Equal(0, engine.GetCurrentFrame(1));
        Assert.Equal("test", engine.GetCurrentMoveId(1));

        for (int i = 0; i < 5; i++) engine.Update();
        Assert.Equal(MovePhase.Active, engine.GetPhase(1));
        Assert.Equal(5, engine.GetCurrentFrame(1));

        for (int i = 0; i < 3; i++) engine.Update();
        Assert.Equal(MovePhase.Recovery, engine.GetPhase(1));
        Assert.Equal(8, engine.GetCurrentFrame(1));

        for (int i = 0; i < 7; i++) engine.Update();
        Assert.Equal(MovePhase.Idle, engine.GetPhase(1));
        Assert.Equal(0, engine.GetCurrentFrame(1));
        Assert.Null(engine.GetCurrentMoveId(1));
    }

    [Fact]
    public void Advantages_AreQueryableFromActiveTimeline()
    {
        var timeline = new MoveTimeline();
        timeline.StartMove(MakeMove("test", 5, 3, 7, hitAdvantage: 2, blockAdvantage: -3, damage: 80));

        Assert.Equal(2, timeline.HitAdvantage);
        Assert.Equal(-3, timeline.BlockAdvantage);
        Assert.Equal(80, timeline.ActiveMove!.Damage);
        Assert.Equal(15, timeline.TotalFrames);
        Assert.Equal("test", timeline.MoveId);
    }

    // --- MoveFrameChanged events (AC 2) ---

    [Fact]
    public void Update_PublishesMoveFrameChanged_WithCorrectFields()
    {
        var (engine, _) = CreateEngine(MakeMove("fireball_c", 5, 3, 7));

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.StartMove(1, "fireball_c");
            engine.Update();
            StepFrame();
        });

        Assert.Single(events);
        var e = events[0];
        Assert.Equal(1, e.PlayerId);
        Assert.Equal("fireball_c", e.MoveId);
        Assert.Equal(0, e.CurrentFrame);
        Assert.Equal(15, e.TotalFrames);
        Assert.Equal(MovePhase.Startup, e.Phase);
    }

    [Fact]
    public void Update_PublishesExactPhaseSequence_ForFullMoveLifetime()
    {
        var (engine, _) = CreateEngine(MakeMove("test", 5, 3, 7));

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.StartMove(1, "test");
            for (int i = 0; i < 20; i++)
            {
                engine.Update();
                StepFrame();
            }
        });

        Assert.Equal(15, events.Count);
        for (int f = 0; f < 15; f++)
        {
            Assert.Equal(f, events[f].CurrentFrame);
            Assert.Equal(15, events[f].TotalFrames);
            var expected = f < 5 ? MovePhase.Startup : f < 8 ? MovePhase.Active : MovePhase.Recovery;
            Assert.Equal(expected, events[f].Phase);
        }
    }

    // --- Dual players (AC 4) ---

    [Fact]
    public void DualPlayers_TimelinesAdvanceIndependently()
    {
        var (engine, _) = CreateEngine(
            MakeMove("p1_move", 5, 3, 7),
            MakeMove("p2_move", 2, 2, 2));

        engine.StartMove(1, "p1_move");
        engine.StartMove(2, "p2_move");

        engine.Update();
        engine.Update();

        Assert.Equal(2, engine.GetCurrentFrame(1));
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
        Assert.Equal(MovePhase.Active, engine.GetPhase(2)); // startup 2 → active at frame 2
    }

    [Fact]
    public void DualPlayers_EventsPublishedForEachActivePlayer()
    {
        var (engine, _) = CreateEngine(
            MakeMove("p1_move", 5, 3, 7),
            MakeMove("p2_move", 2, 2, 2));

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.StartMove(1, "p1_move");
            engine.Update();
            engine.StartMove(2, "p2_move");
            engine.Update();
            StepFrame();
        });

        Assert.Equal(3, events.Count);
        // EventBus dispatches same-type events in LIFO order within one ProcessFrame —
        // assert the set, not the sequence.
        Assert.Contains(events, e => e.PlayerId == 1 && e.CurrentFrame == 0 && e.Phase == MovePhase.Startup);
        Assert.Contains(events, e => e.PlayerId == 1 && e.CurrentFrame == 1 && e.Phase == MovePhase.Startup);
        Assert.Contains(events, e => e.PlayerId == 2 && e.CurrentFrame == 0 && e.Phase == MovePhase.Startup);
    }

    [Fact]
    public void DualPlayers_IdlePlayerPublishesNothing()
    {
        var (engine, _) = CreateEngine(MakeMove("test", 5, 3, 7));

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.StartMove(1, "test");
            engine.Update();
            StepFrame();
        });

        Assert.Single(events);
        Assert.Equal(1, events[0].PlayerId);
        Assert.Equal(MovePhase.Idle, engine.GetPhase(2));
        Assert.Equal(0, engine.GetCurrentFrame(2));
        Assert.Null(engine.GetCurrentMoveId(2));
    }

    // --- Idle state (AC 5) ---

    [Fact]
    public void IdleState_ReturnsNeutralValues()
    {
        var (engine, _) = CreateEngine(MakeMove("test", 5, 3, 7));

        Assert.Equal(MovePhase.Idle, engine.GetPhase(1));
        Assert.Equal(MovePhase.Idle, engine.GetPhase(2));
        Assert.Equal(0, engine.GetCurrentFrame(1));
        Assert.Null(engine.GetCurrentMoveId(1));
    }

    // --- Error handling (AD-8 gameplay errors: log + safe no-op) ---

    [Fact]
    public void StartMove_UnknownMoveId_LogsAndContinues()
    {
        var (engine, _) = CreateEngine(MakeMove("test", 5, 3, 7));

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.StartMove(1, "nonexistent");
            engine.Update();
            StepFrame();
        });

        Assert.Empty(events);
        Assert.Equal(MovePhase.Idle, engine.GetPhase(1));
    }

    [Fact]
    public void StartMove_InvalidPlayerId_LogsAndContinues()
    {
        var (engine, _) = CreateEngine(MakeMove("test", 5, 3, 7));

        var events = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.StartMove(0, "test");
            engine.StartMove(3, "test");
            engine.StartMove(-1, "test");
            engine.Update();
            StepFrame();
        });

        Assert.Empty(events);
        Assert.Equal(MovePhase.Idle, engine.GetPhase(0));
        Assert.Equal(MovePhase.Idle, engine.GetPhase(3));
        Assert.Equal(0, engine.GetCurrentFrame(0));
        Assert.Null(engine.GetCurrentMoveId(99));
    }

    [Fact]
    public void StartMove_WhileActive_IgnoresNewMove()
    {
        var (engine, _) = CreateEngine(
            MakeMove("first", 5, 3, 7),
            MakeMove("second", 2, 2, 2));

        engine.StartMove(1, "first");
        engine.Update();
        engine.StartMove(1, "second");

        Assert.Equal("first", engine.GetCurrentMoveId(1));
        Assert.Equal(1, engine.GetCurrentFrame(1));
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
    }

    // --- Runtime definition change (AC 3) ---

    [Fact]
    public void RuntimeDefinitionChange_TakesEffectOnNextExecution()
    {
        var (engine, store) = CreateEngine(MakeMove("test", 5, 3, 7));

        engine.StartMove(1, "test");
        var first = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.Update();
            StepFrame();
        });
        Assert.Single(first);
        Assert.Equal(15, first[0].TotalFrames);

        // Run the first execution to completion (one Update already done → frame 1).
        for (int i = 0; i < 14; i++) engine.Update();
        StepFrame(); // drain stale events before re-subscribing
        Assert.Equal(MovePhase.Idle, engine.GetPhase(1));

        store.SetMove(MakeMove("test", 2, 2, 2));
        engine.StartMove(1, "test");
        var second = EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            engine.Update();
            StepFrame();
        });
        Assert.Single(second);
        Assert.Equal(6, second[0].TotalFrames);
        Assert.Equal(0, second[0].CurrentFrame);
        Assert.Equal(MovePhase.Startup, second[0].Phase);

        engine.Update(); // frame 1 (startup 2 → still startup), tick to frame 2 → active
        Assert.Equal(MovePhase.Active, engine.GetPhase(1));
    }

    // --- Cancel window integration (Story 2.2, Task 3.10) ---

    private static MoveDefinition MakeWindowedMove(
        string moveId, int startup, int active, int recovery,
        params CancelWindow[] windows) => new()
    {
        MoveId = moveId,
        Startup = startup,
        Active = active,
        Recovery = recovery,
        CancelWindows = new List<CancelWindow>(windows)
    };

    private static CancelWindow W(int startFrame, int endFrame, string targetCategory = "special") => new()
    {
        StartFrame = startFrame,
        EndFrame = endFrame,
        TargetCategory = targetCategory
    };

    [Fact]
    public void CancelWindows_EnteredAndExitedInterleaveWithMoveFrameChanged()
    {
        var (engine, _) = CreateEngine(MakeWindowedMove("test", 5, 3, 7,
            W(3, 7, "special")));

        // Ordered log across per-frame flushes — phase-3 dispatch delivers
        // MoveFrameChanged → Entered → Exited within each frame (AD-4).
        var log = new List<string>();
        void OnMfc(MoveFrameChangedEvent e) => log.Add($"mfc:{e.CurrentFrame}");
        void OnEntered(CancelWindowEnteredEvent e) => log.Add($"entered:{e.Category}:{e.StartFrame}-{e.EndFrame}");
        void OnExited(CancelWindowExitedEvent e) => log.Add($"exited:{e.Category}");
        EventBusTestHelper.Drain();
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(OnMfc);
        EventBus.Instance.Subscribe<CancelWindowEnteredEvent>(OnEntered);
        EventBus.Instance.Subscribe<CancelWindowExitedEvent>(OnExited);
        try
        {
            engine.StartMove(1, "test");
            for (int i = 0; i < 15; i++)
            {
                engine.Update();
                EventBus.Instance.ProcessFrame();
            }
        }
        finally
        {
            EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(OnMfc);
            EventBus.Instance.Unsubscribe<CancelWindowEnteredEvent>(OnEntered);
            EventBus.Instance.Unsubscribe<CancelWindowExitedEvent>(OnExited);
        }

        var expected = new List<string>();
        for (int f = 0; f <= 14; f++)
        {
            expected.Add($"mfc:{f}");
            if (f == 3) expected.Add("entered:special:3-7");
            if (f == 8) expected.Add("exited:special");
        }
        Assert.Equal(expected, log);
    }

    [Fact]
    public void CancelWindows_EmptyWindows_NoCancelEvents()
    {
        var (engine, _) = CreateEngine(MakeMove("test", 5, 3, 7));

        var (mfc, entered, exited) = EventBusTestHelper.Collect<MoveFrameChangedEvent, CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            engine.StartMove(1, "test");
            for (int i = 0; i < 15; i++)
                engine.Update();
        });

        Assert.Equal(15, mfc.Count);
        Assert.Empty(entered);
        Assert.Empty(exited);
    }

    [Fact]
    public void CancelWindows_BothPlayersIndependent()
    {
        var (engine, _) = CreateEngine(
            MakeWindowedMove("p1_move", 5, 3, 7, W(3, 7, "special")),
            MakeWindowedMove("p2_move", 2, 2, 2, W(0, 3, "super")));

        var (mfc, entered, exited) = EventBusTestHelper.Collect<MoveFrameChangedEvent, CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            engine.StartMove(1, "p1_move");
            engine.StartMove(2, "p2_move");
            for (int i = 0; i < 15; i++)
                engine.Update();
        });

        Assert.Contains(mfc, e => e.PlayerId == 1);
        Assert.Contains(mfc, e => e.PlayerId == 2);
        Assert.Contains(entered, e => e.PlayerId == 1 && e.Category == "special");
        Assert.Contains(entered, e => e.PlayerId == 2 && e.Category == "super");
        Assert.Contains(exited, e => e.PlayerId == 1 && e.Category == "special");
        Assert.Contains(exited, e => e.PlayerId == 2 && e.Category == "super");
    }

    [Fact]
    public void CancelWindows_CloseAllWhenMoveCompletes()
    {
        var (engine, _) = CreateEngine(MakeWindowedMove("test", 5, 3, 7,
            W(3, 20, "long_window"))); // window extends beyond move end

        var (_, entered, exited) = EventBusTestHelper.Collect<MoveFrameChangedEvent, CancelWindowEnteredEvent, CancelWindowExitedEvent>(() =>
        {
            engine.StartMove(1, "test");
            for (int i = 0; i < 15; i++)
                engine.Update();
        });

        Assert.Single(entered);
        var exitedSingle = Assert.Single(exited); // CloseAll published Exited
        Assert.Equal("test", exitedSingle.MoveId); // captured pre-Tick, not nulled by completion
        Assert.Equal(MovePhase.Idle, engine.GetPhase(1));
    }
}
