using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public sealed class ControlsLegendViewModelTests
{
    [Fact]
    public void Text_SeparatesGameplayBlockAndDiagnostics()
    {
        var vm = new ControlsLegendViewModel();
        Assert.Contains("P1: A/D move, S crouch, Space jump, U 5LP", vm.GameplayText);
        Assert.Contains("P2: Left/Right move, Down crouch, Up jump, N 5LP", vm.GameplayText);
        Assert.Contains("Back is opposite the opponent-facing direction", vm.BlockText);
        Assert.Contains("Diagnostics (TEST ONLY)", vm.DiagnosticsText);
        Assert.Contains("] step", vm.DiagnosticsText);
        Assert.Contains("[ restore", vm.DiagnosticsText);
    }
}
