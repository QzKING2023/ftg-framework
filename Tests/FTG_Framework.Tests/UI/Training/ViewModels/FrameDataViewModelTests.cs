#nullable enable
using FTG_Framework.Data;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public class FrameDataViewModelTests
{
    private static MoveDefinition MakeMove(
        string moveId, int startup, int active, int recovery) => new()
    {
        MoveId = moveId,
        Startup = startup,
        Active = active,
        Recovery = recovery
    };

    [Fact]
    public void IdlePhase_ShowsIdleText_AndHidesDurations()
    {
        var vm = new FrameDataViewModel();

        vm.Update(null, null, global::FTG_Framework.Core.MovePhase.Idle, 0, 0);

        Assert.Equal("Move: Idle", vm.InfoText);
        Assert.False(vm.DurationsVisible);
    }

    [Fact]
    public void StartupPhase_ShowsWithinPhaseFrameCounter()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", 5, 3, 7));
        var vm = new FrameDataViewModel();

        vm.Update(store, "5LP", global::FTG_Framework.Core.MovePhase.Startup, 3, 15);

        Assert.Contains("5LP", vm.InfoText);
        Assert.Contains("Startup", vm.InfoText);
        Assert.Contains("3/5", vm.InfoText);
        Assert.True(vm.DurationsVisible);
        Assert.Contains("Startup: 5f", vm.DurationsText);
        Assert.Contains("Active: 3f", vm.DurationsText);
        Assert.Contains("Recovery: 7f", vm.DurationsText);
    }

    [Fact]
    public void ActivePhase_SubtractsStartup()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("test", 5, 3, 7));
        var vm = new FrameDataViewModel();

        vm.Update(store, "test", global::FTG_Framework.Core.MovePhase.Active, 6, 15);

        Assert.Contains("1/3", vm.InfoText);
    }

    [Fact]
    public void RecoveryPhase_SubtractsStartupAndActive()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("test", 5, 3, 7));
        var vm = new FrameDataViewModel();

        vm.Update(store, "test", global::FTG_Framework.Core.MovePhase.Recovery, 10, 15);

        Assert.Contains("2/7", vm.InfoText);
    }

    [Fact]
    public void NullDataStore_ShowsUnknown()
    {
        var vm = new FrameDataViewModel();

        vm.Update(null, "missing", global::FTG_Framework.Core.MovePhase.Startup, 0, 10);

        Assert.Contains("(unknown)", vm.InfoText);
        Assert.Contains("missing", vm.InfoText);
        Assert.False(vm.DurationsVisible);
    }

    [Fact]
    public void MoveNotInStore_ShowsUnknown()
    {
        var store = new StubDataStore();
        var vm = new FrameDataViewModel();

        vm.Update(store, "missing", global::FTG_Framework.Core.MovePhase.Startup, 0, 10);

        Assert.Contains("(unknown)", vm.InfoText);
        Assert.False(vm.DurationsVisible);
    }

    [Fact]
    public void ReturnToIdle_AfterMove_ClearsDurations()
    {
        var store = new StubDataStore();
        store.SetMove(MakeMove("5LP", 5, 3, 7));
        var vm = new FrameDataViewModel();

        vm.Update(store, "5LP", global::FTG_Framework.Core.MovePhase.Startup, 0, 15);
        Assert.True(vm.DurationsVisible);

        vm.Update(store, "5LP", global::FTG_Framework.Core.MovePhase.Idle, 0, 0);
        Assert.Equal("Move: Idle", vm.InfoText);
        Assert.False(vm.DurationsVisible);
    }
}
