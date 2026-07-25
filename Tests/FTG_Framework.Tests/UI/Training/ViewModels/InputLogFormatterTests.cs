#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests;

public class InputLogFormatterTests
{
    private static InputLogViewModel.DisplayEntry Dir(int frame, int player, int value) =>
        new(frame, player, InputType.Directional, value);

    private static InputLogViewModel.DisplayEntry Btn(int frame, int player, int value) =>
        new(frame, player, InputType.Button, value);

    private static InputReceivedEvent DirEvent(int frame, int player, int value) =>
        new(player, frame, (int)InputType.Directional, value);

    // --- FrameLevelInputLogFormatter (default) ---

    [Fact]
    public void FrameLevel_OneRowPerEntry_MatchesFormatEntry()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Dir(11, 1, 5),
            Btn(12, 1, 2),
        };

        var rows = FrameLevelInputLogFormatter.Instance.FormatRows(entries);

        Assert.Equal(3, rows.Count);
        for (int i = 0; i < entries.Count; i++)
            Assert.Equal(InputLogViewModel.FormatEntry(entries[i]), rows[i]);
    }

    [Fact]
    public void FrameLevel_EmptyInput_EmptyOutput()
    {
        var rows = FrameLevelInputLogFormatter.Instance.FormatRows(
            new List<InputLogViewModel.DisplayEntry>());

        Assert.Empty(rows);
    }

    // --- MergingInputLogFormatter ---

    [Fact]
    public void Merging_ConsecutiveIdenticalInputs_MergeIntoSingleRow()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Dir(11, 1, 5),
            Dir(12, 1, 5),
        };

        var rows = MergingInputLogFormatter.Instance.FormatRows(entries);

        Assert.Single(rows);
        Assert.Contains("10-12", rows[0]);
        Assert.Contains("P1", rows[0]);
        Assert.Contains("×3", rows[0]);
    }

    [Fact]
    public void Merging_FrameGap_BreaksRun()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Dir(11, 1, 5),
            Dir(15, 1, 5), // gap at 12-14 — not consecutive
        };

        var rows = MergingInputLogFormatter.Instance.FormatRows(entries);

        Assert.Equal(2, rows.Count);
        Assert.Contains("10-11", rows[0]);
        Assert.Contains("×2", rows[0]);
        Assert.Equal(InputLogViewModel.FormatEntry(entries[2]), rows[1]);
    }

    [Fact]
    public void Merging_ValueChange_BreaksRun()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Dir(11, 1, 6), // value changed
        };

        var rows = MergingInputLogFormatter.Instance.FormatRows(entries);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Merging_PlayerChange_BreaksRun()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Dir(11, 2, 5), // player changed
        };

        var rows = MergingInputLogFormatter.Instance.FormatRows(entries);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Merging_TypeChange_BreaksRun()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Btn(11, 1, 5), // type changed
        };

        var rows = MergingInputLogFormatter.Instance.FormatRows(entries);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Merging_MixedSequence_ProducesRunsInOrder()
    {
        var entries = new List<InputLogViewModel.DisplayEntry>
        {
            Dir(10, 1, 5),
            Dir(11, 1, 5),
            Btn(12, 1, 2),
            Dir(13, 1, 6),
            Dir(14, 1, 6),
            Dir(15, 1, 6),
        };

        var rows = MergingInputLogFormatter.Instance.FormatRows(entries);

        Assert.Equal(3, rows.Count);
        Assert.Contains("10-11", rows[0]);
        Assert.Equal(InputLogViewModel.FormatEntry(entries[2]), rows[1]);
        Assert.Contains("13-15", rows[2]);
        Assert.Contains("×3", rows[2]);
    }

    // --- ViewModel integration ---

    [Fact]
    public void ViewModel_DefaultFormatter_PreservesFrameLevelRows()
    {
        var vm = new InputLogViewModel { VisibleRowCount = 10 };
        vm.TryAddEntry(DirEvent(10, 1, 5));
        vm.TryAddEntry(DirEvent(11, 1, 5));

        var rows = vm.GetVisibleRowTexts();

        Assert.Equal(InputLogViewModel.FormatEntry(Dir(10, 1, 5)), rows[0]);
        Assert.Equal(InputLogViewModel.FormatEntry(Dir(11, 1, 5)), rows[1]);
    }

    [Fact]
    public void ViewModel_CustomFormatter_UsedForRowTexts()
    {
        var vm = new InputLogViewModel
        {
            VisibleRowCount = 10,
            Formatter = MergingInputLogFormatter.Instance
        };
        vm.TryAddEntry(DirEvent(10, 1, 5));
        vm.TryAddEntry(DirEvent(11, 1, 5));
        vm.TryAddEntry(DirEvent(12, 1, 5));

        var rows = vm.GetVisibleRowTexts();

        // Three entries merge into one row
        Assert.Equal(1, vm.DisplayRowCount);
        Assert.Contains("10-12", rows[0]);
    }

    [Fact]
    public void ViewModel_MergingFormatter_ScrollClampsToRowCount()
    {
        var vm = new InputLogViewModel
        {
            VisibleRowCount = 2,
            Formatter = MergingInputLogFormatter.Instance
        };
        // 6 entries merge into 3 rows
        vm.TryAddEntry(DirEvent(10, 1, 5));
        vm.TryAddEntry(DirEvent(11, 1, 5));
        vm.TryAddEntry(DirEvent(20, 1, 6));
        vm.TryAddEntry(DirEvent(21, 1, 6));
        vm.TryAddEntry(DirEvent(30, 1, 7));
        vm.TryAddEntry(DirEvent(31, 1, 7));

        Assert.Equal(3, vm.DisplayRowCount);
        Assert.Equal(1, vm.MaxScrollOffset);

        vm.ScrollOffset = 99;
        Assert.Equal(1, vm.ScrollOffset);
    }
}
