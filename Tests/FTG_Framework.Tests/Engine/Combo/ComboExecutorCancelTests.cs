#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using Xunit;

namespace FTG_Framework.Tests;

public class ComboExecutorCancelTests : IDisposable
{
    public ComboExecutorCancelTests()
    {
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        EventBusTestHelper.Drain();
    }

    private static MoveDefinition MakeMove(string moveId, string[] categories, bool chainRepeatable = false) => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
        ChainRepeatable = chainRepeatable
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

    private static (StubDataStore store, ComboExecutor executor) Setup(params GatlingEntry[] entries)
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }));
        store.SetMove(MakeMove("5MP", new[] { "normal" }));
        store.SetMove(MakeMove("5HP", new[] { "normal" }));
        store.SetMove(MakeMove("236P", new[] { "special" }));
        store.SetMove(MakeMove("236236P", new[] { "super" }));
        store.SetGatlingTable(MakeTable("ryu", entries));
        var executor = new ComboExecutor(store);
        executor.Initialize(store);
        return (store, executor);
    }

    private static void OpenWindow(int playerId, string moveId, string category, int start = 3, int end = 6)
    {
        EventBus.Instance.Publish(new CancelWindowEnteredEvent(playerId, moveId, category, start, end));
        EventBus.Instance.ProcessFrame();
    }

    private static void CloseWindow(int playerId, string moveId, string category)
    {
        EventBus.Instance.Publish(new CancelWindowExitedEvent(playerId, moveId, category));
        EventBus.Instance.ProcessFrame();
    }

    private static List<MoveCanceledEvent> CollectMoveCanceled(Action scenario)
    {
        var events = new List<MoveCanceledEvent>();
        void Handler(MoveCanceledEvent e) => events.Add(e);
        EventBus.Instance.Subscribe<MoveCanceledEvent>(Handler);
        try
        {
            scenario();
            EventBus.Instance.ProcessFrame();
        }
        finally
        {
            EventBus.Instance.Unsubscribe<MoveCanceledEvent>(Handler);
        }
        return events;
    }

    // 7.2
    [Fact]
    public void CancelWindowEntered_NoMatch_ReturnsFalse()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"));

        OpenWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.False(executor.TryCancel(1, "236P"));
        });

        Assert.Empty(events);
    }

    // 7.3
    [Fact]
    public void CancelWindowEntered_ValidMatch_ReturnsTrueAndPublishes()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"));

        OpenWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
        });

        Assert.Single(events);
        Assert.Equal(1, events[0].PlayerId);
        Assert.Equal("5LP", events[0].FromMove);
        Assert.Equal("5MP", events[0].ToMove);
        Assert.Equal("normal", events[0].WindowCategory);
    }

    // 7.4
    [Fact]
    public void CancelWindow_WrongCategory_ReturnsFalse()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"));

        OpenWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.False(executor.TryCancel(1, "236P"));
        });

        Assert.Empty(events);
    }

    // 7.5
    [Fact]
    public void CancelWindow_Consumed_CannotFireTwice()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"));

        OpenWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
            Assert.False(executor.TryCancel(1, "5HP"));
        });

        Assert.Single(events);
    }

    // 7.6
    [Fact]
    public void OverlappingWindows_DifferentCategories_Independent()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"),
            MakeEntry("5LP", new[] { "236P" }, "special"));

        OpenWindow(1, "5LP", "normal");
        OpenWindow(1, "5LP", "special");

        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
        });

        Assert.Single(events);
        Assert.Equal("normal", events[0].WindowCategory);

        events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "236P"));
        });

        Assert.Single(events);
        Assert.Equal("special", events[0].WindowCategory);
    }

    // 7.7
    [Fact]
    public void CancelWindowExited_NoCancelAttempt_NoEvent()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP" }, "normal"));

        OpenWindow(1, "5LP", "normal");
        CloseWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.False(executor.TryCancel(1, "5MP"));
        });

        Assert.Empty(events);
    }

    // 7.8
    [Fact]
    public void CancelWindow_EnterThenExit_RemovedFromTracking()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP" }, "normal"));

        OpenWindow(1, "5LP", "normal");
        CloseWindow(1, "5LP", "normal");

        Assert.False(executor.TryCancel(1, "5MP"));
    }

    // 7.9
    [Fact]
    public void NoActiveWindow_TryCancel_ReturnsFalse()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP" }, "normal"));

        var events = CollectMoveCanceled(() =>
        {
            Assert.False(executor.TryCancel(1, "5MP"));
        });

        Assert.Empty(events);
    }

    // 7.10
    [Fact]
    public void MultipleInputsInSameWindow_OnlyFirstConsumes()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP", "5HP" }, "normal"));

        OpenWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
            Assert.False(executor.TryCancel(1, "5HP"));
        });

        Assert.Single(events);
        Assert.Equal("5MP", events[0].ToMove);
    }

    // 7.11
    [Fact]
    public void WindowSnapshot_UsedForGatlingLookup()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }));
        store.SetMove(MakeMove("5MP", new[] { "normal" }));
        store.SetMove(MakeMove("5HP", new[] { "normal" }));

        // Table A: 5LP → 5MP (normal)
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5MP" }, "normal")));

        var executor = new ComboExecutor(store);
        executor.Initialize(store);

        OpenWindow(1, "5LP", "normal");

        // Swap to table B: 5LP → 5HP (normal) — 5MP no longer a valid target
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5HP" }, "normal")));

        // Should still use window snapshot (table A), so 5MP is a valid target
        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
        });

        Assert.Single(events);
    }

    [Fact]
    public void TryCancel_NullMoveId_ReturnsFalse()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP" }, "normal"));

        Assert.False(executor.TryCancel(1, null!));
    }

    [Fact]
    public void TryCancel_InvalidPlayerId_ReturnsFalse()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP" }, "normal"));

        Assert.False(executor.TryCancel(99, "5MP"));
    }

    [Fact]
    public void CancelWindow_Player2SeparateTracking()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }));
        store.SetMove(MakeMove("5MP", new[] { "normal" }));
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5MP" }, "normal")));
        store.SetGatlingTable(MakeTable("ken",
            MakeEntry("5LP", new[] { "5MP" }, "normal")));

        var executor = new ComboExecutor(store);
        executor.Initialize(store);

        OpenWindow(1, "5LP", "normal");
        // No window for player 2

        Assert.True(executor.TryCancel(1, "5MP")); // P1: cancel works
        Assert.False(executor.TryCancel(2, "5MP")); // P2: no window
    }

    // Chain integration tests (Story 3.3)

    // 4.2
    [Fact]
    public void TryCancel_DuplicateInChain_RejectedWindowConsumed()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }));
        store.SetMove(MakeMove("5MP", new[] { "normal" }));
        store.SetMove(MakeMove("5HP", new[] { "normal" }));
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5MP" }, "normal"),
            MakeEntry("5MP", new[] { "5HP" }, "normal"),
            MakeEntry("5HP", new[] { "5MP" }, "normal")));

        var executor = new ComboExecutor(store);
        executor.Initialize(store);

        // Build chain: 5LP→5MP
        OpenWindow(1, "5LP", "normal");
        Assert.True(executor.TryCancel(1, "5MP"));

        // Build chain: 5MP→5HP (chain is now conceptually [5LP, 5MP, 5HP])
        OpenWindow(1, "5MP", "normal");
        Assert.True(executor.TryCancel(1, "5HP"));

        // Try to cancel back to 5MP (already in chain)
        OpenWindow(1, "5HP", "normal");
        var events = CollectMoveCanceled(() =>
        {
            Assert.False(executor.TryCancel(1, "5MP"));
        });

        Assert.Empty(events); // No MoveCanceled published
        // Window should be consumed — TryCancel again should fail
        Assert.False(executor.TryCancel(1, "5MP"));
    }

    // 4.3
    [Fact]
    public void TryCancel_ChainRepeatable_DuplicateAllowed()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }, chainRepeatable: true));
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5LP" }, "normal")));

        var executor = new ComboExecutor(store);
        executor.Initialize(store);

        // First cancel: 5LP→5LP
        OpenWindow(1, "5LP", "normal");
        var events1 = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5LP"));
        });
        Assert.Single(events1);

        // Second cancel: 5LP→5LP (chain-repeatable allows it)
        OpenWindow(1, "5LP", "normal");
        var events2 = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5LP"));
        });
        Assert.Single(events2);
    }

    // 4.4
    [Fact]
    public void TryCancel_EmptyChain_FirstCancelSucceeds()
    {
        var (_, executor) = Setup(
            MakeEntry("5LP", new[] { "5MP" }, "normal"));

        OpenWindow(1, "5LP", "normal");

        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
        });

        Assert.Single(events);
    }

    // 4.5
    [Fact]
    public void ComboEndedEvent_ResetsChain()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }));
        store.SetMove(MakeMove("5MP", new[] { "normal" }));
        store.SetMove(MakeMove("5HP", new[] { "normal" }));
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5MP" }, "normal"),
            MakeEntry("5MP", new[] { "5HP" }, "normal"),
            MakeEntry("5HP", new[] { "5MP" }, "normal")));

        var executor = new ComboExecutor(store);
        executor.Initialize(store);

        // Build chain: 5LP→5MP→5HP
        OpenWindow(1, "5LP", "normal");
        executor.TryCancel(1, "5MP");
        OpenWindow(1, "5MP", "normal");
        executor.TryCancel(1, "5HP");

        // 5MP blocked (in chain)
        OpenWindow(1, "5HP", "normal");
        Assert.False(executor.TryCancel(1, "5MP"));

        // ComboEndedEvent resets the chain
        EventBus.Instance.Publish(new ComboEndedEvent(1, 3, "5HP"));
        EventBusTestHelper.Drain();

        // Now 5MP should be allowed again
        OpenWindow(1, "5HP", "normal");
        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(1, "5MP"));
        });

        Assert.Single(events);
    }

    // 4.6
    [Fact]
    public void ComboEndedEvent_WrongPlayer_DoesNotAffectOtherPlayer()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", new[] { "normal" }));
        store.SetMove(MakeMove("5MP", new[] { "normal" }));
        store.SetMove(MakeMove("5HP", new[] { "normal" }));
        store.SetGatlingTable(MakeTable("ryu",
            MakeEntry("5LP", new[] { "5MP" }, "normal"),
            MakeEntry("5MP", new[] { "5HP" }, "normal"),
            MakeEntry("5HP", new[] { "5MP" }, "normal")));
        store.SetGatlingTable(MakeTable("ken",
            MakeEntry("5HP", new[] { "5LP" }, "normal"),
            MakeEntry("5LP", new[] { "5MP" }, "normal")));

        var executor = new ComboExecutor(store);
        executor.Initialize(store);

        // Player 1 chain: 5LP→5MP→5HP
        OpenWindow(1, "5LP", "normal");
        executor.TryCancel(1, "5MP");
        OpenWindow(1, "5MP", "normal");
        executor.TryCancel(1, "5HP");

        // Player 2 chain: 5HP→5LP
        OpenWindow(2, "5HP", "normal");
        executor.TryCancel(2, "5LP");

        // Publish ComboEndedEvent for player 2
        EventBus.Instance.Publish(new ComboEndedEvent(2, 2, "5LP"));
        EventBusTestHelper.Drain();

        // Player 1's chain should be unchanged — 5MP still blocked
        OpenWindow(1, "5HP", "normal");
        Assert.False(executor.TryCancel(1, "5MP"));

        // Player 2's chain should be reset — 5LP allowed again
        OpenWindow(2, "5LP", "normal");
        var events = CollectMoveCanceled(() =>
        {
            Assert.True(executor.TryCancel(2, "5MP"));
        });

        Assert.Single(events);
    }
}
