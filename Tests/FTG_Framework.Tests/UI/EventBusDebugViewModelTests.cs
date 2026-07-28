#nullable enable
using FTG_Framework.Core;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI;

public sealed class EventBusDebugViewModelTests
{
    private static EventBusDebugViewModel CreateViewModel(bool enabled = true)
    {
        var service = new EventBusDebugService();
        var vm = new EventBusDebugViewModel(service);
        if (enabled)
        {
            vm.ToggleVisibility(); // enables the service
        }
        return vm;
    }

    private static void TearDown(EventBusDebugViewModel vm)
    {
        if (vm.IsVisible)
            vm.ToggleVisibility();
    }

    [Fact]
    public void SortedEntries_Recency_OrdersByLastFrameSeenDescending()
    {
        var vm = CreateViewModel();
        try
        {
            vm.SortMode = "recency";

            EventBus.Instance.ProcessFrame();
            vm.Refresh();

            var sorted = vm.SortedEntries;
            Assert.NotEmpty(sorted);
            for (int i = 1; i < sorted.Count; i++)
            {
                Assert.True(sorted[i - 1].LastFrameSeen >= sorted[i].LastFrameSeen);
            }
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void SortedEntries_Name_OrdersAlphabetically()
    {
        var vm = CreateViewModel();
        try
        {
            vm.SortMode = "name";

            EventBus.Instance.ProcessFrame();
            vm.Refresh();

            var sorted = vm.SortedEntries;
            Assert.NotEmpty(sorted);
            for (int i = 1; i < sorted.Count; i++)
            {
                Assert.True(string.Compare(sorted[i - 1].EventTypeName, sorted[i].EventTypeName,
                    StringComparison.Ordinal) <= 0);
            }
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void SortedEntries_Count_OrdersByTotalOccurrencesDescending()
    {
        var vm = CreateViewModel();
        try
        {
            vm.SortMode = "count";

            EventBus.Instance.ProcessFrame();
            vm.Refresh();

            var sorted = vm.SortedEntries;
            Assert.NotEmpty(sorted);
            for (int i = 1; i < sorted.Count; i++)
            {
                Assert.True(sorted[i - 1].TotalOccurrences >= sorted[i].TotalOccurrences);
            }
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void ToggleVisibility_EnablesAndDisablesService()
    {
        var service = new EventBusDebugService();
        var vm = new EventBusDebugViewModel(service);

        Assert.False(vm.IsVisible);
        Assert.False(service.Enabled);

        vm.ToggleVisibility();
        try
        {
            Assert.True(vm.IsVisible);
            Assert.True(service.Enabled);
        }
        finally
        {
            vm.ToggleVisibility();
        }

        Assert.False(vm.IsVisible);
        Assert.False(service.Enabled);
    }

    [Fact]
    public void CycleSortMode_RotatesThroughAllModes()
    {
        var vm = CreateViewModel();
        try
        {
            Assert.Equal("recency", vm.SortMode);

            vm.CycleSortMode();
            Assert.Equal("name", vm.SortMode);

            vm.CycleSortMode();
            Assert.Equal("count", vm.SortMode);

            vm.CycleSortMode();
            Assert.Equal("recency", vm.SortMode);
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void SelectedEntry_ReturnsNull_WhenIndexIsNegative()
    {
        var vm = CreateViewModel();
        try
        {
            vm.SelectedIndex = -1;
            vm.Refresh();

            Assert.Null(vm.SelectedEntry);
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void SelectedEntry_ReturnsNull_WhenIndexOutOfRange()
    {
        var vm = CreateViewModel();
        try
        {
            vm.SelectedIndex = 999;
            vm.Refresh();

            Assert.Null(vm.SelectedEntry);
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void SelectedDetailText_ShowsPlaceholder_WhenNoSelection()
    {
        var vm = CreateViewModel();
        try
        {
            vm.SelectedIndex = -1;

            var detail = vm.SelectedDetailText;
            Assert.Contains("No event selected", detail);
        }
        finally
        {
            TearDown(vm);
        }
    }

    [Fact]
    public void FormatPayload_TruncatesLongStrings()
    {
        var longPayload = new string('X', 1000);
        var result = EventBusDebugViewModel.FormatPayload(longPayload);

        var lines = result.Split('\n');
        Assert.True(lines.Length <= 10, $"Expected <= 10 lines, got {lines.Length}");
        Assert.Contains("(truncated)", result);
    }

    [Fact]
    public void FormatPayload_PreservesShortStrings()
    {
        var shortPayload = "HitConnectedEvent { AttackerId = 1, DefenderId = 2 }";
        var result = EventBusDebugViewModel.FormatPayload(shortPayload);

        Assert.Equal(shortPayload, result);
        Assert.DoesNotContain("(truncated)", result);
    }

    [Fact]
    public void FormatPayload_HandlesEmptyString()
    {
        var result = EventBusDebugViewModel.FormatPayload("");
        Assert.Equal("(empty)", result);
    }

    [Fact]
    public void FormatPayload_HandlesNullString()
    {
        var result = EventBusDebugViewModel.FormatPayload(null!);
        Assert.Equal("(empty)", result);
    }

    [Fact]
    public void SelectedDetailText_IncludesSubscriberCountAndFrameInfo()
    {
        var vm = CreateViewModel();
        try
        {
            EventBus.Instance.ProcessFrame();
            vm.Refresh();

            vm.SelectedIndex = 0;
            var detail = vm.SelectedDetailText;

            Assert.Contains("Subscribers:", detail);
            Assert.Contains("Last Frame:", detail);
            Assert.Contains("Total Occurrences:", detail);
            Assert.Contains("Payload", detail);
        }
        finally
        {
            TearDown(vm);
        }
    }
}
