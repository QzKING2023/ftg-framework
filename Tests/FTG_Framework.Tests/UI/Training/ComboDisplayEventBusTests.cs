#nullable enable
using System;
using System.Reflection;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

[Collection(EventBusTestCollection.Name)]
public sealed class ComboDisplayEventBusTests : System.IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();

    public void Dispose() => _eventBusScope.Dispose();

    [Fact]
    public void ProcessFrame_HitThenLaterComboPhaseEnd_FinalStateIsReset()
    {
        var vm = new ComboDisplayViewModel();
        using var controller = new ComboDisplayController(vm);
        controller.Start();

        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 30, 5, "a"));
        EventBus.Instance.Publish(new ComboEndedEvent(1, 1, "5LP"));
        EventBus.Instance.ProcessFrame();

        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
    }

    [Fact]
    public void LifecycleEvents_ResetAllBuckets()
    {
        var vm = new ComboDisplayViewModel();
        using var controller = new ComboDisplayController(vm);
        controller.Start();

        Hit(vm);
        EventBus.Instance.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
        Hit(vm);
        EventBus.Instance.PublishImmediate(new StateRestoredEvent(10, 2));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
        Hit(vm);
        EventBus.Instance.PublishImmediate(new ReplayStartedEvent(20, 1));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
        Hit(vm);
        EventBus.Instance.PublishImmediate(new ReplayEndedEvent(20));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
    }

    [Fact]
    public void StartAndShutdown_AreIdempotentAndDoNotDuplicateHits()
    {
        int baseline = EventBus.Instance.GetSubscriberCount<HitConnectedEvent>();
        var vm = new ComboDisplayViewModel();
        var controller = new ComboDisplayController(vm);

        controller.Start();
        controller.Start();
        Assert.Equal(baseline + 1, EventBus.Instance.GetSubscriberCount<HitConnectedEvent>());
        EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "5LP", 3, 30, 5, "a"));
        Assert.Equal(1, vm.GetState(1).HitCount);

        controller.Shutdown();
        controller.Shutdown();
        Assert.Equal(baseline, EventBus.Instance.GetSubscriberCount<HitConnectedEvent>());
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
    }

    [Fact]
    public void ShutdownBeforeQueuedDispatch_PreventsStaleMutation()
    {
        var vm = new ComboDisplayViewModel();
        using var controller = new ComboDisplayController(vm);
        controller.Start();
        EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 3, 30, 5, "a"));

        controller.Shutdown();
        EventBus.Instance.ProcessFrame();

        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
    }

    [Fact]
    public void LifecycleReset_InvalidatesCapturedHandlerAndRebindsCurrentHandler()
    {
        var vm = new ComboDisplayViewModel();
        using var controller = new ComboDisplayController(vm);
        controller.Start();
        var field = typeof(ComboDisplayController).GetField(
            "_hitHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stale = (Action<HitConnectedEvent>)field.GetValue(controller)!;

        EventBus.Instance.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
        stale(new HitConnectedEvent(1, 2, "5LP", 3, 30, 5, "a"));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));

        EventBus.Instance.PublishImmediate(new HitConnectedEvent(1, 2, "5LP", 3, 30, 6, "a"));
        Assert.Equal(1, vm.GetState(1).HitCount);
    }

    private static void Hit(ComboDisplayViewModel vm)
    {
        Assert.True(vm.HandleHit(new HitConnectedEvent(1, 2, "5LP", 3, 30, 5, "a")));
    }
}
