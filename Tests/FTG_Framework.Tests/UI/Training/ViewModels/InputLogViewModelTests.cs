#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public class InputLogViewModelTests
{
    private static InputLogViewModel CreateVm(
        bool showP1 = true, bool showP2 = true, int visibleRowCount = 10, int capacity = 600)
    {
        return new InputLogViewModel
        {
            ShowP1 = showP1,
            ShowP2 = showP2,
            VisibleRowCount = visibleRowCount,
            Capacity = capacity
        };
    }

    [Fact]
    public void TryAddEntry_AppendsAndReturnsTrue()
    {
        var vm = CreateVm();

        var added = vm.TryAddEntry(new InputReceivedEvent(1, 100, (int)InputType.Directional, 6));

        Assert.True(added);
        Assert.Equal(1, vm.EntryCount);
        Assert.Equal(100, vm.Entries[0].Frame);
        Assert.Equal(InputType.Directional, vm.Entries[0].Type);
        Assert.Equal(6, vm.Entries[0].Value);
    }

    [Fact]
    public void TryAddEntry_Duplicate_ReturnsFalse()
    {
        var vm = CreateVm();

        vm.TryAddEntry(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));
        var added = vm.TryAddEntry(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));

        Assert.False(added);
        Assert.Equal(1, vm.EntryCount);
    }

    [Fact]
    public void TryAddEntry_InvalidPlayerId_ReturnsFalse()
    {
        var vm = CreateVm();

        var added = vm.TryAddEntry(new InputReceivedEvent(3, 10, (int)InputType.Directional, 5));

        Assert.False(added);
        Assert.Equal(0, vm.EntryCount);
    }

    [Fact]
    public void TryAddEntry_InvalidInputType_ReturnsFalse()
    {
        var vm = CreateVm();

        var added = vm.TryAddEntry(new InputReceivedEvent(1, 10, 7, 5));

        Assert.False(added);
        Assert.Equal(0, vm.EntryCount);
    }

    [Fact]
    public void ShowP1_False_HidesP1Entries()
    {
        var vm = CreateVm(showP1: false, showP2: true);

        vm.TryAddEntry(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        vm.TryAddEntry(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));

        Assert.Equal(2, vm.EntryCount);
        Assert.Equal(1, vm.FilteredCount);
    }

    [Fact]
    public void ShowP2_False_HidesP2Entries()
    {
        var vm = CreateVm(showP1: true, showP2: false);

        vm.TryAddEntry(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        vm.TryAddEntry(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));

        Assert.Equal(1, vm.FilteredCount);
    }

    [Fact]
    public void ShowP1_Toggle_RestoresEntries()
    {
        var vm = CreateVm(showP1: true, showP2: true);

        vm.TryAddEntry(new InputReceivedEvent(1, 1, (int)InputType.Directional, 6));
        vm.TryAddEntry(new InputReceivedEvent(2, 2, (int)InputType.Button, 0));
        Assert.Equal(2, vm.FilteredCount);

        vm.ShowP1 = false;
        Assert.Equal(1, vm.FilteredCount);

        vm.ShowP1 = true;
        Assert.Equal(2, vm.FilteredCount);
    }

    [Fact]
    public void CapacityEviction_RemovesOldestEntries()
    {
        var vm = CreateVm(capacity: 600);

        for (int i = 0; i < 610; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        Assert.Equal(600, vm.EntryCount);
        Assert.Equal(10, vm.Entries[0].Frame);
        Assert.Equal(609, vm.Entries[599].Frame);
    }

    [Fact]
    public void Eviction_CountBased_FrameJumpDoesNotEvict()
    {
        var vm = CreateVm();

        vm.TryAddEntry(new InputReceivedEvent(1, 0, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 1, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 2, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 700, (int)InputType.Directional, 5));

        Assert.Equal(4, vm.EntryCount);
    }

    [Fact]
    public void Eviction_PerTrack_KeepsNewestWithinTrackOnly()
    {
        var vm = CreateVm(capacity: 600);

        for (int i = 0; i < 605; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 700, (int)InputType.Button, 0));
        vm.TryAddEntry(new InputReceivedEvent(2, 701, (int)InputType.Directional, 6));

        Assert.Equal(602, vm.EntryCount);
        Assert.Equal(5, vm.Entries[0].Frame);
    }

    [Fact]
    public void ScrollOffset_ClampsToValidRange()
    {
        var vm = CreateVm(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        vm.ScrollOffset = 10;
        Assert.Equal(2, vm.ScrollOffset);

        vm.ScrollOffset = -5;
        Assert.Equal(0, vm.ScrollOffset);
    }

    [Fact]
    public void Display_DefaultOffset_ShowsNewestEntries()
    {
        var vm = CreateVm(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        var rows = vm.GetVisibleRowTexts();
        Assert.Equal(3, rows.Count);
        Assert.Contains("   4  P1", rows[2]);
        Assert.Contains("   3  P1", rows[1]);
        Assert.Contains("   2  P1", rows[0]);
    }

    [Fact]
    public void Display_OffsetScrolledUp_ShowsOlderEntries()
    {
        var vm = CreateVm(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        vm.ScrollOffset = 2;

        var rows = vm.GetVisibleRowTexts();
        Assert.Contains("   0  P1", rows[0]);
        Assert.Contains("   1  P1", rows[1]);
        Assert.Contains("   2  P1", rows[2]);
    }

    [Fact]
    public void InitialSnapshot_LoadsFromInputHistory()
    {
        var stub = new StubInputHistory();
        stub.AddDirectionalEntry(1, 10, DirectionValue.Forward);
        stub.AddDirectionalEntry(1, 20, DirectionValue.Down);
        stub.AddButtonEntry(1, 15, ButtonValue.A);

        var vm = new InputLogViewModel();
        vm.LoadInitialSnapshot(stub, 1, true, true);

        Assert.Equal(3, vm.EntryCount);
    }

    [Fact]
    public void InitialSnapshot_NullInputHistory_StartsEmpty()
    {
        var vm = new InputLogViewModel();
        vm.LoadInitialSnapshot(null, 1, true, true);

        Assert.Equal(0, vm.EntryCount);
    }

    [Fact]
    public void RewindToFrame_RemovesFutureEntries()
    {
        var vm = CreateVm();

        vm.TryAddEntry(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));
        vm.TryAddEntry(new InputReceivedEvent(1, 20, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 30, (int)InputType.Directional, 6));

        vm.RewindToFrame(15);

        Assert.Equal(1, vm.EntryCount);
        Assert.Equal(10, vm.Entries[0].Frame);
    }

    [Fact]
    public void DisplayFrameLimit_FiltersFutureFrames()
    {
        var vm = CreateVm();

        vm.TryAddEntry(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));
        vm.TryAddEntry(new InputReceivedEvent(1, 20, (int)InputType.Button, 0));

        vm.DisplayFrameLimit = 10;

        Assert.Equal(2, vm.EntryCount);
        Assert.Equal(1, vm.FilteredCount);
        var rows = vm.GetVisibleRowTexts();
        Assert.Contains("  10  P1", rows[0]);
    }

    [Fact]
    public void VisibleRowCount_LimitsDisplayedRows()
    {
        var vm = CreateVm(visibleRowCount: 3);

        for (int i = 0; i < 10; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        var rows = vm.GetVisibleRowTexts();
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void VisibleRowCount_LessEntriesThanRows_ReturnsFewerFilledRows()
    {
        var vm = CreateVm(visibleRowCount: 10);

        vm.TryAddEntry(new InputReceivedEvent(1, 1, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 2, (int)InputType.Directional, 5));

        var rows = vm.GetVisibleRowTexts();
        Assert.Equal(10, rows.Count);
        Assert.NotEmpty(rows[0]);
        Assert.NotEmpty(rows[1]);
        Assert.Equal("", rows[2]);
    }

    [Fact]
    public void VisibleRowCount_OutOfRange_Clamped()
    {
        var vm = CreateVm(visibleRowCount: 100);
        Assert.Equal(50, vm.VisibleRowCount);

        vm.VisibleRowCount = 0;
        Assert.Equal(1, vm.VisibleRowCount);
    }

    // --- Formatting ---

    [Fact]
    public void FormatDirection_AllStandardValues()
    {
        for (int i = 1; i <= 9; i++)
            Assert.Equal(i.ToString(), InputLogViewModel.FormatDirection(i));
        Assert.Equal("?0", InputLogViewModel.FormatDirection(0));
        Assert.Equal("?10", InputLogViewModel.FormatDirection(10));
    }

    [Fact]
    public void FormatButton_AllStandardValues()
    {
        Assert.Equal("A", InputLogViewModel.FormatButton(0));
        Assert.Equal("B", InputLogViewModel.FormatButton(1));
        Assert.Equal("C", InputLogViewModel.FormatButton(2));
        Assert.Equal("D", InputLogViewModel.FormatButton(3));
        Assert.Equal("?5", InputLogViewModel.FormatButton(5));
    }

    [Fact]
    public void FormatEntry_ContainsFramePlayerTypeAndValue()
    {
        var entry = new InputLogViewModel.DisplayEntry(42, 1, InputType.Button, 0);
        var result = InputLogViewModel.FormatEntry(entry);

        Assert.Contains("42", result);
        Assert.Contains("P1", result);
        Assert.Contains("B", result);
        Assert.Contains("A", result);
    }

    [Fact]
    public void FormatEntry_DirectionalInput_UsesNumpadNotation()
    {
        var entry = new InputLogViewModel.DisplayEntry(1, 1, InputType.Directional, 6);
        var result = InputLogViewModel.FormatEntry(entry);
        Assert.Contains("   1  P1  D  6", result);
    }

    [Fact]
    public void MaxScrollOffset_CalculatesCorrectly()
    {
        var vm = CreateVm(visibleRowCount: 3);

        for (int i = 0; i < 5; i++)
            vm.TryAddEntry(new InputReceivedEvent(1, i, (int)InputType.Directional, 5));

        Assert.Equal(2, vm.MaxScrollOffset);
    }

    // --- Converted from Control-level InputLogTests ---

    [Fact]
    public void TryAddEntry_MultipleEntries_MaintainsInsertionOrder()
    {
        var vm = CreateVm();

        vm.TryAddEntry(new InputReceivedEvent(1, 10, (int)InputType.Directional, 6));
        vm.TryAddEntry(new InputReceivedEvent(1, 20, (int)InputType.Directional, 5));
        vm.TryAddEntry(new InputReceivedEvent(1, 15, (int)InputType.Directional, 6));

        Assert.Equal(3, vm.EntryCount);
        Assert.Equal(10, vm.Entries[0].Frame);
        Assert.Equal(20, vm.Entries[1].Frame);
        Assert.Equal(15, vm.Entries[2].Frame);
    }

    [Fact]
    public void InitialSnapshot_LoadsTrackedPlayer_EvenWhenToggleOff()
    {
        var stub = new StubInputHistory();
        stub.AddDirectionalEntry(1, 10, DirectionValue.Forward);
        stub.AddDirectionalEntry(2, 11, DirectionValue.Back);

        var vm = new InputLogViewModel { ShowP1 = false, ShowP2 = true };
        vm.LoadInitialSnapshot(stub, trackedPlayer: 1, showP1: false, showP2: true);

        Assert.Equal(2, vm.EntryCount);
        Assert.Equal(1, vm.FilteredCount);
        Assert.DoesNotContain("P1", vm.GetVisibleRowTexts()[0]);
    }
}
