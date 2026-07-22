#nullable enable
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public class AdvantageViewModelTests
{
    [Fact]
    public void HitConnected_TrackedPlayerIsAttacker_SetsPositiveAdvantage()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnHitConnected(1, 2, 4);

        Assert.Equal(4, vm.Advantage);
        Assert.Equal("+4", vm.DisplayText);
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public void HitConnected_TrackedPlayerIsDefender_SetsNegativeAdvantage()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnHitConnected(2, 1, 4);

        Assert.Equal(-4, vm.Advantage);
        Assert.Equal("-4", vm.DisplayText);
    }

    [Fact]
    public void MoveBlocked_TrackedPlayerIsAttacker_SetsNegativeAdvantage()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnMoveBlocked(1, 2, -5);

        Assert.Equal(-5, vm.Advantage);
        Assert.Equal("-5", vm.DisplayText);
    }

    [Fact]
    public void MoveBlocked_TrackedPlayerIsDefender_SetsPositiveAdvantage()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnMoveBlocked(2, 1, -5);

        Assert.Equal(5, vm.Advantage);
        Assert.Equal("+5", vm.DisplayText);
    }

    [Fact]
    public void HitConnected_ZeroAdvantage_DisplaysZero()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnHitConnected(1, 2, 0);

        Assert.Equal(0, vm.Advantage);
        Assert.Equal("0", vm.DisplayText);
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public void FrameAdvanced_DecrementsPositiveAdvantage_StepsToZero()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnHitConnected(1, 2, 4);
        Assert.Equal(4, vm.Advantage);

        for (int expected = 3; expected >= 0; expected--)
        {
            vm.OnFrameAdvanced();
            Assert.Equal(expected, vm.Advantage);
        }

        vm.OnFrameAdvanced();
        Assert.Equal(0, vm.Advantage);
        Assert.Equal("0", vm.DisplayText);
    }

    [Fact]
    public void FrameAdvanced_IncrementsNegativeAdvantage_StepsToZero()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnMoveBlocked(1, 2, -5);
        Assert.Equal(-5, vm.Advantage);

        for (int expected = -4; expected <= 0; expected++)
        {
            vm.OnFrameAdvanced();
            Assert.Equal(expected, vm.Advantage);
        }

        vm.OnFrameAdvanced();
        Assert.Equal(0, vm.Advantage);
    }

    [Fact]
    public void HitConnected_MidCountdown_ResetsAdvantage()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnHitConnected(1, 2, 4);
        vm.OnFrameAdvanced();
        vm.OnFrameAdvanced();
        Assert.Equal(2, vm.Advantage);

        vm.OnHitConnected(1, 2, 6);
        Assert.Equal(6, vm.Advantage);
        Assert.Equal("+6", vm.DisplayText);
    }

    [Fact]
    public void ShowWhenZero_False_HidesWhenZero()
    {
        var vm = new AdvantageViewModel(1, showWhenZero: false);

        Assert.False(vm.IsVisible);

        vm.OnHitConnected(1, 2, 3);
        Assert.True(vm.IsVisible);

        vm.OnFrameAdvanced();
        vm.OnFrameAdvanced();
        vm.OnFrameAdvanced();
        Assert.Equal(0, vm.Advantage);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void ShowWhenZero_True_ShowsWhenZero()
    {
        var vm = new AdvantageViewModel(1, showWhenZero: true);

        Assert.True(vm.IsVisible);
        Assert.Equal("0", vm.DisplayText);
    }

    [Fact]
    public void TrackedPlayer_Two_UsesP2Perspective()
    {
        var vm = new AdvantageViewModel(2);

        vm.OnHitConnected(1, 2, 4);
        Assert.Equal(-4, vm.Advantage);
        Assert.Equal("-4", vm.DisplayText);

        vm.OnHitConnected(2, 1, 4);
        Assert.Equal(4, vm.Advantage);
        Assert.Equal("+4", vm.DisplayText);
    }

    [Fact]
    public void HitConnected_NeitherAttackerNorDefender_AdvantageUnchanged()
    {
        var vm = new AdvantageViewModel(1);

        vm.OnHitConnected(1, 2, 4);
        Assert.Equal(4, vm.Advantage);

        vm.OnHitConnected(3, 4, 10);
        Assert.Equal(4, vm.Advantage);
    }
}
