#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public class FrameDataEngineCancelTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Subscribers | EventBusResidualState.Queues);
    public FrameDataEngineCancelTests()
    {
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
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

    private static StubDataStore SetupStore()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", 5, 3, 7, MakeWindow(3, 6, "normal")));
        store.SetMove(MakeMove("5MP", 4, 3, 8, MakeWindow(2, 5, "normal")));
        store.SetMove(MakeMove("5HP", 8, 4, 12, MakeWindow(4, 7, "special")));
        return store;
    }

    // 8.2
    [Fact]
    public void InterruptAndStart_FromActiveMove_TransitionsCorrectly()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);

        engine.StartMove(1, "5LP");
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));

        engine.InterruptAndStart(1, "5MP");

        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
        Assert.Equal("5MP", engine.GetCurrentMoveId(1));
        Assert.Equal(0, engine.GetCurrentFrame(1));
    }

    // 8.3
    [Fact]
    public void InterruptAndStart_FromIdle_StartsNormally()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);

        // No move started — character is idle. InterruptAndStart falls back to StartMove.
        engine.InterruptAndStart(1, "5MP");

        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
        Assert.Equal("5MP", engine.GetCurrentMoveId(1));
        Assert.Equal(0, engine.GetCurrentFrame(1));
    }

    // 8.4
    [Fact]
    public void InterruptAndStart_NullMove_NoOp()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);

        engine.StartMove(1, "5LP");
        string? originalMove = engine.GetCurrentMoveId(1);

        engine.InterruptAndStart(1, "nonexistent");

        // Move should be unchanged
        Assert.Equal(originalMove, engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
    }

    // 8.5
    [Fact]
    public void InterruptAndStart_ResetsCancelTracker()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);

        engine.StartMove(1, "5LP");

        // Advance to frame 3, where cancel window should be open
        engine.Update(); // frame 0→1
        engine.Update(); // frame 1→2
        engine.Update(); // frame 2→3 — this is where CancelWindowEntered fires

        // Process the cancel window events
        EventBus.Instance.ProcessFrame();

        // Now interrupt
        engine.InterruptAndStart(1, "5MP");

        // Advance the new move — should not trigger old cancel windows
        engine.Update();
        EventBus.Instance.ProcessFrame();

        Assert.Equal("5MP", engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
    }

    // 8.6
    [Fact]
    public void MoveCanceledEvent_Handler_TriggersInterrupt()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);
        engine.Initialize(store);

        engine.StartMove(1, "5LP");
        Assert.Equal("5LP", engine.GetCurrentMoveId(1));

        // Publish MoveCanceled via EventBus — handler should call InterruptAndStart
        EventBus.Instance.Publish(new MoveCanceledEvent(1, "5LP", "5MP", "normal"));
        EventBus.Instance.ProcessFrame();

        Assert.Equal("5MP", engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
        Assert.Equal(0, engine.GetCurrentFrame(1));
    }

    // 8.7
    [Fact]
    public void MoveCanceledEvent_WrongPlayer_NoEffect()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);
        engine.Initialize(store);

        engine.StartMove(1, "5LP");

        // Publish MoveCanceled for player 2 — should NOT affect player 1
        EventBus.Instance.Publish(new MoveCanceledEvent(2, "5LP", "5MP", "normal"));
        EventBus.Instance.ProcessFrame();

        Assert.Equal("5LP", engine.GetCurrentMoveId(1));
        Assert.Equal(MovePhase.Startup, engine.GetPhase(1));
    }

    [Fact]
    public void InterruptAndStart_InvalidPlayerId_NoOp()
    {
        var store = SetupStore();
        var engine = new FrameDataEngine(store);

        engine.StartMove(1, "5LP");
        engine.InterruptAndStart(99, "5MP"); // Invalid player

        Assert.Equal("5LP", engine.GetCurrentMoveId(1)); // Unchanged
    }
}
