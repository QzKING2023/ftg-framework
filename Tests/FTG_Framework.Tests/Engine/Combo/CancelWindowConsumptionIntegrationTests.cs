#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests;

public class CancelWindowConsumptionIntegrationTests : IDisposable
{
    public CancelWindowConsumptionIntegrationTests()
    {
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        EventBusTestHelper.Drain();
    }

    private static MoveDefinition MakeMove(string moveId, int startup = 5, int active = 3, int recovery = 7,
        params CancelWindow[] windows) => new()
    {
        MoveId = moveId,
        Startup = startup,
        Active = active,
        Recovery = recovery,
        CancelWindows = new List<CancelWindow>(windows)
    };

    private static CancelWindow MakeWindow(int start, int end, string category = "normal") => new()
    {
        StartFrame = start,
        EndFrame = end,
        TargetCategory = category
    };

    private static GatlingTable MakeTable(string characterId, params GatlingEntry[] entries) => new()
    {
        CharacterId = characterId,
        Entries = new List<GatlingEntry>(entries)
    };

    private static GatlingEntry MakeEntry(string source, string[] targets, string category = "normal") => new()
    {
        SourceMove = source,
        TargetMoves = new List<string>(targets),
        CancelCategory = category
    };

    private class TestHarness
    {
        public StubDataStore Store { get; }
        public FrameDataEngine Engine { get; }
        public ComboExecutor Executor { get; }

        public TestHarness()
        {
            Store = new StubDataStore();
            Store.SetMove(MakeMove("5LP", 5, 3, 7, MakeWindow(3, 6, "normal")));
            Store.SetMove(MakeMove("5MP", 4, 3, 8, MakeWindow(2, 5, "normal")));
            Store.SetMove(MakeMove("5HP", 8, 4, 12, MakeWindow(4, 7, "special")));
            Store.SetMove(MakeMove("nonexistent_target", 1, 1, 1));
            Store.SetGatlingTable(MakeTable("ryu",
                MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"),
                MakeEntry("5MP", new[] { "5HP" }, "normal")));

            Engine = new FrameDataEngine(Store);
            Engine.Initialize(Store);

            Executor = new ComboExecutor(Store);
            Executor.Initialize(Store);
        }

        public void OpenCancelWindow(int playerId, string moveId, string category, int start = 3, int end = 6)
        {
            EventBus.Instance.Publish(new CancelWindowEnteredEvent(playerId, moveId, category, start, end));
            EventBus.Instance.ProcessFrame();
        }

        public void CloseCancelWindow(int playerId, string moveId, string category)
        {
            EventBus.Instance.Publish(new CancelWindowExitedEvent(playerId, moveId, category));
            EventBus.Instance.ProcessFrame();
        }

        public void ProcessOneFrame()
        {
            EventBus.Instance.ProcessFrame();
        }
    }

    // 9.2
    [Fact]
    public void FullCancelPipeline_InterruptsActiveMove()
    {
        var h = new TestHarness();

        h.Engine.StartMove(1, "5LP");
        Assert.Equal("5LP", h.Engine.GetCurrentMoveId(1));

        h.OpenCancelWindow(1, "5LP", "normal");

        Assert.True(h.Executor.TryCancel(1, "5MP"));

        // MoveCanceled event dispatched → FrameDataEngine interrupt
        h.ProcessOneFrame();

        Assert.Equal("5MP", h.Engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, h.Engine.GetPhase(1));
        Assert.Equal(0, h.Engine.GetCurrentFrame(1));
    }

    // 9.3
    [Fact]
    public void NoCancel_WhenWindowExpired()
    {
        var h = new TestHarness();

        h.Engine.StartMove(1, "5LP");
        h.OpenCancelWindow(1, "5LP", "normal");
        h.CloseCancelWindow(1, "5LP", "normal");

        Assert.False(h.Executor.TryCancel(1, "5MP"));
        h.ProcessOneFrame();

        // Move should be unchanged
        Assert.Equal("5LP", h.Engine.GetCurrentMoveId(1));
    }

    // 9.4
    [Fact]
    public void ChainedCancel_TwoCancelsInSequence()
    {
        var h = new TestHarness();

        // First cancel: 5LP → 5MP
        h.Engine.StartMove(1, "5LP");
        h.OpenCancelWindow(1, "5LP", "normal");
        Assert.True(h.Executor.TryCancel(1, "5MP"));
        h.ProcessOneFrame();
        Assert.Equal("5MP", h.Engine.GetCurrentMoveId(1));

        // Advance 5MP to enter its cancel window
        h.Engine.Update(); // 0→1
        h.Engine.Update(); // 1→2 — window opens at frame 2
        h.ProcessOneFrame(); // Dispatch cancel window events

        h.OpenCancelWindow(1, "5MP", "normal");

        // Second cancel: 5MP → 5HP
        Assert.True(h.Executor.TryCancel(1, "5HP"));
        h.ProcessOneFrame();

        Assert.Equal("5HP", h.Engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, h.Engine.GetPhase(1));
    }

    // 9.5
    [Fact]
    public void Cancel_FromMove_ToMove_InterruptsCorrectly()
    {
        var h = new TestHarness();

        h.Engine.StartMove(1, "5LP");

        // Advance to frame 3 before canceling
        h.Engine.Update(); // 0→1
        h.Engine.Update(); // 1→2
        h.Engine.Update(); // 2→3

        h.OpenCancelWindow(1, "5LP", "normal");

        Assert.True(h.Executor.TryCancel(1, "5MP"));
        h.ProcessOneFrame();

        // New move starts from frame 0, old progress is lost
        Assert.Equal("5MP", h.Engine.GetCurrentMoveId(1));
        Assert.Equal(0, h.Engine.GetCurrentFrame(1));
    }

    [Fact]
    public void Cancel_WithMoveNotFound_TimelineUnchanged()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", 5, 3, 7, MakeWindow(3, 6, "normal")));
        store.SetMove(MakeMove("5MP", 4, 3, 8));

        var engine = new FrameDataEngine(store);
        engine.Initialize(store);
        engine.StartMove(1, "5LP");

        engine.InterruptAndStart(1, "ghost_move");

        // Timeline should not change — the move doesn't exist
        Assert.Equal("5LP", engine.GetCurrentMoveId(1));
    }
}
