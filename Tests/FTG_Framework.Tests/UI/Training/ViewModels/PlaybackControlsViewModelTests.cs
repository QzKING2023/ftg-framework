#nullable enable
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public class PlaybackControlsViewModelTests
{
    [Fact]
    public void InitialState_ShowsRunningAtFrameZero()
    {
        var vm = new PlaybackControlsViewModel();

        Assert.Equal(0, vm.DisplayFrame);
        Assert.False(vm.Paused);
        Assert.Equal("Frame: 0 [RUNNING]", vm.DisplayText);
    }

    [Fact]
    public void OnFrameAdvanced_UpdatesFrameNumber()
    {
        var vm = new PlaybackControlsViewModel();

        vm.OnFrameAdvanced(42);

        Assert.Equal(42, vm.DisplayFrame);
        Assert.Contains("42", vm.DisplayText);
    }

    [Fact]
    public void SetPaused_UpdatesLabel()
    {
        var vm = new PlaybackControlsViewModel();

        vm.OnFrameAdvanced(10);
        vm.SetPaused(true);

        Assert.True(vm.Paused);
        Assert.Equal("Frame: 10 [PAUSED]", vm.DisplayText);
    }

    [Fact]
    public void SetPaused_False_RestoresRunning()
    {
        var vm = new PlaybackControlsViewModel();

        vm.SetPaused(true);
        vm.SetPaused(false);

        Assert.False(vm.Paused);
        Assert.Contains("RUNNING", vm.DisplayText);
    }

    [Fact]
    public void Toggle_FlipsPauseState()
    {
        var vm = new PlaybackControlsViewModel();

        vm.SetPaused(true);
        Assert.True(vm.Paused);

        vm.SetPaused(false);
        Assert.False(vm.Paused);
    }

    [Fact]
    public void OnStepBackward_UpdatesDisplayFrame()
    {
        var vm = new PlaybackControlsViewModel();

        vm.OnFrameAdvanced(10);
        vm.SetPaused(true);
        vm.OnStepBackward(9);

        Assert.Equal(9, vm.DisplayFrame);
    }

    [Fact]
    public void CanStepBackward_AtFrameZero_ReturnsFalse()
    {
        var vm = new PlaybackControlsViewModel();

        Assert.False(vm.CanStepBackward(-1));
    }

    [Fact]
    public void CanStepBackward_BeforeEarliestSnapshot_ReturnsFalse()
    {
        var vm = new PlaybackControlsViewModel();
        vm.OnFrameAdvanced(5);

        Assert.False(vm.CanStepBackward(5));
    }

    [Fact]
    public void CanStepBackward_AtOrAfterEarliestSnapshot_ReturnsTrue()
    {
        var vm = new PlaybackControlsViewModel();
        vm.OnFrameAdvanced(5);

        Assert.True(vm.CanStepBackward(4));
        Assert.False(vm.CanStepBackward(5));
        Assert.True(vm.CanStepBackward(-1));
    }
}
