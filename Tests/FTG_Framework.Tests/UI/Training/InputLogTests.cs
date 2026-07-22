#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

// Frame simulation model: EventBus.ProcessFrame() auto-injects one FrameAdvancedEvent.
// One ProcessFrame() call == one frame. Never publish FrameAdvancedEvent manually.
// InputReceivedEvent carries its own Frame number (from the input system) and
// dispatches in Phase 2 (Input System events). The InputLog processes it
// same-frame via _OnInputReceived.
public class InputLogTests : IDisposable
{
    private readonly List<InputLog> _panels = new();

    private InputLog CreatePanel(
        int trackedPlayer = 1,
        bool showP1 = true,
        bool showP2 = true,
        int visibleRowCount = 10,
        StubInputHistory? stubHistory = null)
    {
        EventBusTestHelper.Drain();
        var panel = new InputLog
        {
            TrackedPlayer = trackedPlayer,
            ShowP1 = showP1,
            ShowP2 = showP2,
            VisibleRowCount = visibleRowCount,
            InputHistory = stubHistory
        };
        panel._Ready();
        _panels.Add(panel);
        return panel;
    }

    public void Dispose()
    {
        foreach (var panel in _panels)
            panel._ExitTree();
        EventBusTestHelper.Drain();
    }

    private static void PublishAndProcess<T>(T evt) where T : struct
    {
        EventBus.Instance.Publish(evt);
        EventBus.Instance.ProcessFrame();
    }

    // --- 3.2: InputReceivedEvent appends entry ---

    [Fact]
    public void InputReceived_AppendsEntry_DisplayShowsFormattedRow()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 100, (int)InputType.Directional, 6));

        Assert.Equal(1, panel.EntryCount);
        Assert.Contains("P1", panel.DisplayText);
        Assert.Contains("D", panel.DisplayText);
        Assert.Contains("6", panel.DisplayText);
        Assert.Contains("100", panel.DisplayText);
        Assert.Equal(1, panel.VisibleLabelCount);
        Assert.True(panel.IsBackgroundVisible);
    }

    [Fact]
    public void InputReceived_MultipleEntries_MaintainsInsertionOrder()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));
        PublishAndProcess(new InputReceivedEvent(1, 20, (int)InputType.Button, 0));
        PublishAndProcess(new InputReceivedEvent(2, 15, (int)InputType.Directional, 2));

        Assert.Equal(3, panel.EntryCount);
        var entries = panel.Entries;
        Assert.Equal(10, entries[0].Frame);
        Assert.Equal(20, entries[1].Frame);
        Assert.Equal(15, entries[2].Frame);
    }

    // --- 3.4: Entry format ---

    [Fact]
    public void Format_DirectionalInput_UsesNumpadNotation()
    {
        var panel = CreatePanel();

        // DirectionValue enum uses numpad positions directly as int values.
        // Assert full line format so frame numbers can't false-match the value column.
        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        Assert.Contains("   1  P1  D  6", panel.DisplayText);

        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Directional, 2));
        Assert.Contains("   2  P1  D  2", panel.DisplayText);

        PublishAndProcess(new InputReceivedEvent(1, 3, (int)InputType.Directional, 5));
        Assert.Contains("   3  P1  D  5", panel.DisplayText);
    }

    [Fact]
    public void Format_ButtonInput_UsesButtonNames()
    {
        var panel = CreatePanel();

        // ButtonValue: A=0, B=1, C=2, D=3
        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Button, 0));
        Assert.Contains("A", panel.DisplayText);

        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Button, 3));
        Assert.Contains("D", panel.DisplayText);
    }

    [Fact]
    public void Format_UnknownDirectionValue_ShowsQuestionMarkPrefix()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 99));

        Assert.Contains("?99", panel.DisplayText);
    }

    [Fact]
    public void Format_UnknownButtonValue_ShowsQuestionMarkPrefix()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Button, 99));

        Assert.Contains("?99", panel.DisplayText);
    }

    [Fact]
    public void Format_EntryLine_ContainsFramePlayerTypeAndValue()
    {
        var result = InputLog.FormatEntry(new InputLogViewModel.DisplayEntry(42, 1, InputType.Button, 0));

        Assert.Contains("42", result);
        Assert.Contains("P1", result);
        Assert.Contains("B", result);
        Assert.Contains("A", result);
    }

    // --- 3.5: VisibleRowCount ---

    [Fact]
    public void VisibleRowCount_LimitsDisplayedLabels()
    {
        var panel = CreatePanel(visibleRowCount: 3);

        for (int i = 0; i < 10; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        Assert.Equal(10, panel.EntryCount);
        Assert.Equal(3, panel.VisibleLabelCount);
    }

    [Fact]
    public void VisibleRowCount_LessEntriesThanRows_FewerLabelsVisible()
    {
        var panel = CreatePanel(visibleRowCount: 10);

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 5));
        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Directional, 5));

        Assert.Equal(2, panel.EntryCount);
        Assert.Equal(2, panel.VisibleLabelCount);
    }

    // --- 3.6: Player toggles ---

    [Fact]
    public void ShowP1_False_HidesP1Entries()
    {
        var panel = CreatePanel(showP1: false, showP2: true);

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        PublishAndProcess(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));

        Assert.Equal(2, panel.EntryCount);
        Assert.Equal(1, panel.FilteredCount);
        Assert.Contains("P2", panel.DisplayText);
        Assert.Contains("A", panel.DisplayText);
        Assert.DoesNotContain("P1", panel.DisplayText);
    }

    [Fact]
    public void ShowP2_False_HidesP2Entries()
    {
        var panel = CreatePanel(showP1: true, showP2: false);

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        PublishAndProcess(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));

        Assert.Equal(1, panel.FilteredCount);
        Assert.Contains("P1", panel.DisplayText);
        Assert.DoesNotContain("P2", panel.DisplayText);
    }

    [Fact]
    public void ShowP1_Toggle_RestoresEntries()
    {
        var panel = CreatePanel(showP1: true, showP2: true);

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        PublishAndProcess(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));

        Assert.Equal(2, panel.FilteredCount);

        panel.ShowP1 = false;
        panel._RefreshDisplay();

        Assert.Equal(1, panel.FilteredCount);
        Assert.DoesNotContain("P1", panel.DisplayText);

        panel.ShowP1 = true;
        panel._RefreshDisplay();

        Assert.Equal(2, panel.FilteredCount);
        Assert.Contains("P1", panel.DisplayText);
    }

    // --- 3.7: Both players ---

    [Fact]
    public void BothPlayers_Interleaved_ShowsCorrectPlayerLabels()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        PublishAndProcess(new InputReceivedEvent(2, 2, (int)InputType.Directional, 2));

        var text = panel.DisplayText;
        Assert.Contains("P1", text);
        Assert.Contains("P2", text);
    }

    // --- 3.8: Capacity eviction ---

    [Fact]
    public void CapacityEviction_RemovesOldestEntries()
    {
        const int capacity = 600;
        var stub = new StubInputHistory();
        var panel = CreatePanel(stubHistory: stub);

        Assert.Equal(capacity, panel.Capacity);

        // Add capacity + 10 entries with increasing frame numbers
        for (int i = 0; i < capacity + 10; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        Assert.Equal(capacity, panel.EntryCount);

        // Oldest entries (frames 0-9) should be evicted
        var entries = panel.Entries;
        Assert.Equal(10, entries[0].Frame);
        Assert.Equal(capacity + 9, entries[capacity - 1].Frame);
    }

    [Fact]
    public void Eviction_CountBased_FrameJumpDoesNotEvict()
    {
        var panel = CreatePanel();

        // Eviction mirrors InputHistory's per-track count, not a frame window:
        // a forward frame jump must not evict entries while the track is below capacity.
        PublishAndProcess(new InputReceivedEvent(1, 0, (int)InputType.Directional, 5));
        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 5));
        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Directional, 5));
        PublishAndProcess(new InputReceivedEvent(1, 700, (int)InputType.Directional, 5));

        Assert.Equal(4, panel.EntryCount);
    }

    [Fact]
    public void Eviction_PerTrack_KeepsNewestWithinTrackOnly()
    {
        const int capacity = 600;
        var stub = new StubInputHistory();
        var panel = CreatePanel(stubHistory: stub);

        // Overflow only P1's directional track.
        for (int i = 0; i < capacity + 5; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));
        PublishAndProcess(new InputReceivedEvent(1, 700, (int)InputType.Button, 0));
        PublishAndProcess(new InputReceivedEvent(2, 701, (int)InputType.Directional, 6));

        // Directional track trimmed to capacity (frames 5..604); other tracks untouched.
        Assert.Equal(capacity + 2, panel.EntryCount);
        var entries = panel.Entries;
        Assert.Equal(5, entries[0].Frame);
        Assert.Equal(700, entries[capacity].Frame);
        Assert.Equal(701, entries[capacity + 1].Frame);
    }

    // --- 3.9: ScrollOffset ---

    [Fact]
    public void ScrollOffset_ClampsToValidRange()
    {
        var panel = CreatePanel(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        // 5 entries, 3 visible → max offset = 2
        panel.ScrollOffset = 10;
        Assert.Equal(2, panel.ScrollOffset);

        panel.ScrollOffset = -5;
        Assert.Equal(0, panel.ScrollOffset);
    }

    [Fact]
    public void Display_DefaultOffset_ShowsNewestEntries()
    {
        var panel = CreatePanel(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        // Offset 0 anchors to the bottom: newest 3 entries visible, oldest scrolled off.
        var text = panel.DisplayText;
        Assert.Contains("   4  P1", text);
        Assert.Contains("   3  P1", text);
        Assert.Contains("   2  P1", text);
        Assert.DoesNotContain("   0  P1", text);
        Assert.DoesNotContain("   1  P1", text);
    }

    [Fact]
    public void Display_NewEntry_AppearsAtBottomSameFrame()
    {
        var panel = CreatePanel(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        var lines = panel.DisplayText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("   4  P1", lines[2]);
    }

    [Fact]
    public void Display_OffsetScrolledUp_ShowsOlderEntries()
    {
        var panel = CreatePanel(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        panel.ScrollOffset = 2; // max — oldest window

        var text = panel.DisplayText;
        Assert.Contains("   0  P1", text);
        Assert.Contains("   1  P1", text);
        Assert.Contains("   2  P1", text);
        Assert.DoesNotContain("   3  P1", text);
        Assert.DoesNotContain("   4  P1", text);
    }

    // --- 3.10: Initial state ---

    [Fact]
    public void InitialState_EmptyLog_NoVisibleLabels()
    {
        var panel = CreatePanel();

        Assert.Equal(0, panel.EntryCount);
        Assert.Equal(0, panel.VisibleLabelCount);
        Assert.Equal(0, panel.FilteredCount);
    }

    [Fact]
    public void InitialSnapshot_LoadsFromInputHistory()
    {
        var stub = new StubInputHistory();
        stub.AddDirectionalEntry(1, 10, DirectionValue.Forward);
        stub.AddDirectionalEntry(1, 20, DirectionValue.Down);
        stub.AddButtonEntry(1, 15, ButtonValue.A);

        var panel = CreatePanel(stubHistory: stub);

        Assert.Equal(3, panel.EntryCount);
        Assert.Contains("P1", panel.DisplayText);
        Assert.Contains("6", panel.DisplayText); // Forward → 6
        Assert.Contains("A", panel.DisplayText);
    }

    [Fact]
    public void InitialSnapshot_NullInputHistory_StartsEmpty()
    {
        var panel = CreatePanel(stubHistory: null);

        Assert.Equal(0, panel.EntryCount);
        Assert.Equal(600, panel.Capacity);
    }

    [Fact]
    public void InitialSnapshot_LoadsTrackedPlayer_EvenWhenToggleOff()
    {
        var stub = new StubInputHistory();
        stub.AddDirectionalEntry(1, 10, DirectionValue.Forward);
        stub.AddDirectionalEntry(2, 11, DirectionValue.Back);

        // TrackedPlayer=1 with P1 hidden: P1 history still loads (but stays display-filtered).
        var panel = CreatePanel(trackedPlayer: 1, showP1: false, showP2: true, stubHistory: stub);

        Assert.Equal(2, panel.EntryCount);
        Assert.Equal(1, panel.FilteredCount);
        Assert.DoesNotContain("P1", panel.DisplayText);
    }

    [Fact]
    public void DuplicateEvent_MatchingSnapshotEntry_NotDuplicated()
    {
        var stub = new StubInputHistory();
        stub.AddDirectionalEntry(1, 10, DirectionValue.Forward);
        var panel = CreatePanel(stubHistory: stub);

        Assert.Equal(1, panel.EntryCount);

        // RecordInput writes synchronously but dispatches on the next ProcessFrame;
        // a panel readied in between must not append the same entry twice.
        PublishAndProcess(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));

        Assert.Equal(1, panel.EntryCount);
    }

    [Fact]
    public void InvalidPayload_Ignored()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(3, 1, (int)InputType.Directional, 5));
        PublishAndProcess(new InputReceivedEvent(1, 2, 7, 5));

        Assert.Equal(0, panel.EntryCount);
    }

    [Fact]
    public void VisibleRowCount_RuntimeChange_RecreatesLabels()
    {
        var panel = CreatePanel(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            PublishAndProcess(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));
        Assert.Equal(3, panel.VisibleLabelCount);

        panel.VisibleRowCount = 5;
        Assert.Equal(5, panel.VisibleLabelCount);
        Assert.Contains("   0  P1", panel.DisplayText);

        panel.VisibleRowCount = 2;
        Assert.Equal(2, panel.VisibleLabelCount);
        Assert.DoesNotContain("   2  P1", panel.DisplayText);
    }

    [Fact]
    public void VisibleRowCount_OutOfRange_Clamped()
    {
        var panel = CreatePanel(visibleRowCount: 100);

        Assert.Equal(50, panel.VisibleRowCount);
    }

    // --- 3.11: ExitTree cleanup ---

    [Fact]
    public void ExitTree_Unsubscribes_NoMoreUpdates()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        Assert.Equal(1, panel.EntryCount);

        panel._ExitTree();
        EventBusTestHelper.Drain();

        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Button, 0));

        Assert.Equal(1, panel.EntryCount);
    }

    // --- 3.12: Interleaved P1/P2 entries ---

    [Fact]
    public void InterleavedP1P2_ShowsCorrectPlayerPrefix()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        PublishAndProcess(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));
        PublishAndProcess(new InputReceivedEvent(1, 3, (int)InputType.Button, 1));

        var text = panel.DisplayText;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(lines, l => l.Contains("P1") && l.Contains("6"));
        Assert.Contains(lines, l => l.Contains("P2") && l.Contains("A"));
        Assert.Contains(lines, l => l.Contains("P1") && l.Contains("B"));
    }

    // --- FormatDirection coverage ---

    [Fact]
    public void FormatDirection_AllStandardValues()
    {
        // DirectionValue uses numpad positions as int values directly: 1-9
        Assert.Equal("1", InputLog.FormatDirection(1));
        Assert.Equal("2", InputLog.FormatDirection(2));
        Assert.Equal("3", InputLog.FormatDirection(3));
        Assert.Equal("4", InputLog.FormatDirection(4));
        Assert.Equal("5", InputLog.FormatDirection(5));
        Assert.Equal("6", InputLog.FormatDirection(6));
        Assert.Equal("7", InputLog.FormatDirection(7));
        Assert.Equal("8", InputLog.FormatDirection(8));
        Assert.Equal("9", InputLog.FormatDirection(9));
        Assert.Equal("?0", InputLog.FormatDirection(0));
        Assert.Equal("?10", InputLog.FormatDirection(10));
    }

    // --- FormatButton coverage ---

    [Fact]
    public void FormatButton_AllStandardValues()
    {
        Assert.Equal("A", InputLog.FormatButton(0));
        Assert.Equal("B", InputLog.FormatButton(1));
        Assert.Equal("C", InputLog.FormatButton(2));
        Assert.Equal("D", InputLog.FormatButton(3));
        Assert.Equal("?5", InputLog.FormatButton(5));
    }

    // --- _Ready re-entry guard ---

    [Fact]
    public void Ready_Reentry_DoesNotDuplicateLabelsOrSubscription()
    {
        var panel = CreatePanel(visibleRowCount: 5);

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        Assert.Equal(1, panel.VisibleLabelCount);

        // Call _Ready again — re-entry guard should prevent duplicate children,
        // and EventBus dedupes the re-subscribe.
        panel._Ready();

        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Button, 0));

        // Exactly one new entry: a double-subscribe would append it twice.
        Assert.Equal(2, panel.EntryCount);
        Assert.Equal(2, panel.VisibleLabelCount);
    }

    [Fact]
    public void ReenterTree_AfterExit_Resubscribes()
    {
        var panel = CreatePanel();

        PublishAndProcess(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        panel._ExitTree();

        // Node re-enters the tree: Godot calls _Ready again, which must re-subscribe.
        panel._Ready();
        PublishAndProcess(new InputReceivedEvent(1, 2, (int)InputType.Directional, 5));

        Assert.Equal(2, panel.EntryCount);
    }
}
