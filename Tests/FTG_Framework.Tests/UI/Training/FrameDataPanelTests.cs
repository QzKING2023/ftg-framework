#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

public class FrameDataPanelTests : IDisposable
{
    private FrameDataPanel? _panel;

    private static MoveDefinition MakeMove(
        string moveId, int startup, int active, int recovery) => new()
    {
        MoveId = moveId,
        Startup = startup,
        Active = active,
        Recovery = recovery
    };

    private (FrameDataPanel panel, StubDataStore store) CreatePanel(
        int trackedPlayer = 1,
        params MoveDefinition[] moves)
    {
        var store = new StubDataStore();
        foreach (var m in moves)
            store.SetMove(m);

        var panel = new FrameDataPanel
        {
            TrackedPlayer = trackedPlayer,
            DataStore = store
        };
        panel._Ready();
        _panel = panel;

        return (panel, store);
    }

    public void Dispose()
    {
        _panel?._ExitTree();
        EventBusTestHelper.Drain();
    }

    // --- 3.2: MoveFrameChangedEvent for tracked player updates display ---

    [Fact]
    public void MoveFrameChanged_ForTrackedPlayer_UpdatesDisplay()
    {
        var (panel, _) = CreatePanel(1, MakeMove("5LP", 5, 3, 7));

        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 0, 15, MovePhase.Startup));
        });

        Assert.Contains("5LP", panel.InfoText);
        Assert.Contains(MovePhase.Startup.ToString(), panel.InfoText);
        Assert.Contains("0/5", panel.InfoText);
        Assert.True(panel.DurationsVisible);
        Assert.Contains("Startup: 5f", panel.DurationsText);
        Assert.Contains("Active: 3f", panel.DurationsText);
        Assert.Contains("Recovery: 7f", panel.DurationsText);
    }

    [Fact]
    public void MoveFrameChanged_UpdatesWithinPhaseFrameCounter()
    {
        var (panel, _) = CreatePanel(1, MakeMove("5LP", 5, 3, 7));

        // Frame 6 = Active phase, within-phase frame = 6 - 5 = 1
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 6, 15, MovePhase.Active));
        });

        Assert.Contains("1/3", panel.InfoText);
        Assert.Contains(MovePhase.Active.ToString(), panel.InfoText);
    }

    // --- 3.3: MoveFrameChangedEvent for OTHER player does NOT update display ---

    [Fact]
    public void MoveFrameChanged_ForOtherPlayer_DoesNotUpdateDisplay()
    {
        var (panel, _) = CreatePanel(1, MakeMove("5LP", 5, 3, 7));

        // Initial state
        Assert.Contains("Idle", panel.InfoText);

        // P2 event when tracking P1
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(2, "2M", 0, 10, MovePhase.Startup));
        });

        Assert.Contains("Idle", panel.InfoText);
    }

    // --- 3.4: Idle phase shows "Idle" text and hides per-phase duration line ---

    [Fact]
    public void IdlePhase_ShowsIdleText_AndHidesDurations()
    {
        var (panel, _) = CreatePanel(1, MakeMove("5LP", 5, 3, 7));

        // Start with a move
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 0, 15, MovePhase.Startup));
        });

        Assert.Contains("5LP", panel.InfoText);
        Assert.True(panel.DurationsVisible);

        // Return to idle
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 0, 0, MovePhase.Idle));
        });

        Assert.Equal("Move: Idle", panel.InfoText);
        Assert.False(panel.DurationsVisible);
    }

    [Fact]
    public void InitialState_BeforeAnyEvent_ShowsIdle()
    {
        var (panel, _) = CreatePanel(1);

        Assert.Equal("Move: Idle", panel.InfoText);
        Assert.False(panel.DurationsVisible);
    }

    // --- 3.5: Per-phase frame counter computes correctly ---

    [Fact]
    public void PerPhaseFrameCounter_StartupPhase_EqualsCurrentFrame()
    {
        var (panel, _) = CreatePanel(1, MakeMove("test", 5, 3, 7));

        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "test", 3, 15, MovePhase.Startup));
        });

        Assert.Contains("3/5", panel.InfoText);
    }

    [Fact]
    public void PerPhaseFrameCounter_ActivePhase_SubtractsStartup()
    {
        var (panel, _) = CreatePanel(1, MakeMove("test", 5, 3, 7));

        // Frame 6: 6 - 5 = 1 in active
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "test", 6, 15, MovePhase.Active));
        });

        Assert.Contains("1/3", panel.InfoText);
    }

    [Fact]
    public void PerPhaseFrameCounter_RecoveryPhase_SubtractsStartupAndActive()
    {
        var (panel, _) = CreatePanel(1, MakeMove("test", 5, 3, 7));

        // Frame 10: 10 - 5 - 3 = 2 in recovery
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "test", 10, 15, MovePhase.Recovery));
        });

        Assert.Contains("2/7", panel.InfoText);
    }

    // --- 3.6: Panel respects _trackedPlayer switch ---

    [Fact]
    public void TrackedPlayer_SwitchFromP1ToP2_P1IgnoredP2Honored()
    {
        var (panel, store) = CreatePanel(1,
            MakeMove("p1_move", 5, 3, 7),
            MakeMove("p2_move", 2, 2, 2));

        // P1 event when tracking P1 — should update
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "p1_move", 0, 15, MovePhase.Startup));
        });

        Assert.Contains("p1_move", panel.InfoText);

        // Switch to P2
        panel.TrackedPlayer = 2;

        // P1 event when tracking P2 — should NOT update
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "p1_move", 3, 15, MovePhase.Startup));
        });

        Assert.Contains("p1_move", panel.InfoText); // unchanged

        // P2 event when tracking P2 — should update
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(2, "p2_move", 0, 6, MovePhase.Startup));
        });

        Assert.Contains("p2_move", panel.InfoText);
    }

    // --- 3.7: Panel cleanup — after _ExitTree(), published events do not mutate the panel's labels ---

    [Fact]
    public void ExitTree_Unsubscribes_PanelNotUpdatedByEvents()
    {
        var (panel, _) = CreatePanel(1, MakeMove("5LP", 5, 3, 7));

        // Verify subscription works
        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 0, 15, MovePhase.Startup));
        });

        Assert.Contains("5LP", panel.InfoText);

        // Unsubscribe
        panel._ExitTree();

        // Drain any remaining events
        EventBusTestHelper.Drain();

        // Publish event — panel should not react
        EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "5LP", 10, 15, MovePhase.Recovery));
        EventBus.Instance.ProcessFrame();

        // Text should still show the old state
        Assert.Contains("5LP", panel.InfoText);
        Assert.DoesNotContain("Recovery", panel.InfoText);
    }

    // --- Edge cases ---

    [Fact]
    public void DataStoreNull_ShowsUnknown()
    {
        var panel = new FrameDataPanel { TrackedPlayer = 1, DataStore = null };
        panel._Ready();

        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "unknown_move", 0, 10, MovePhase.Startup));
        });

        Assert.Contains("(unknown)", panel.InfoText);
        Assert.Contains("unknown_move", panel.InfoText);
        Assert.False(panel.DurationsVisible);
    }

    [Fact]
    public void GetMoveReturnsNull_ShowsUnknown()
    {
        var (panel, _) = CreatePanel(1); // no moves registered

        EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
        {
            EventBus.Instance.Publish(new MoveFrameChangedEvent(1, "missing", 0, 10, MovePhase.Startup));
        });

        Assert.Contains("(unknown)", panel.InfoText);
        Assert.Contains("missing", panel.InfoText);
        Assert.False(panel.DurationsVisible);
    }
}