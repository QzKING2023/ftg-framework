#nullable enable
using FTG_Framework.UI.Training.ViewModels;
using Godot;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public class HitboxOverlayViewModelTests
{
    [Fact]
    public void SetHitboxes_AddsBoxes()
    {
        var vm = new HitboxOverlayViewModel();

        vm.SetHitboxes(new Rect2[]
        {
            new(10, 20, 30, 40),
            new(50, 60, 70, 80)
        });

        Assert.Equal(2, vm.BoxCount);
    }

    [Fact]
    public void SetHurtboxes_AddsBoxes()
    {
        var vm = new HitboxOverlayViewModel();

        vm.SetHurtboxes(new Rect2[] { new(5, 5, 10, 20) });

        Assert.Equal(1, vm.BoxCount);
    }

    [Fact]
    public void SetHitboxes_ReplacesPreviousHitboxes_DoesNotAccumulate()
    {
        var vm = new HitboxOverlayViewModel();

        vm.SetHitboxes(new Rect2[] { new(0, 0, 10, 10), new(20, 20, 30, 30) });
        vm.SetHitboxes(new Rect2[] { new(5, 5, 10, 10) });

        Assert.Equal(1, vm.BoxCount);
        Assert.Equal(new Rect2(5, 5, 10, 10), vm.Boxes[0].Rect);
    }

    [Fact]
    public void SetHitboxes_DoesNotReplaceHurtboxes()
    {
        var vm = new HitboxOverlayViewModel();

        vm.SetHurtboxes(new Rect2[] { new(5, 5, 10, 20) });
        vm.SetHitboxes(new Rect2[] { new(0, 0, 10, 10) });

        Assert.Equal(2, vm.BoxCount);
    }

    [Fact]
    public void Clear_RemovesAllBoxes()
    {
        var vm = new HitboxOverlayViewModel();

        vm.SetHitboxes(new[] { new Rect2(0, 0, 10, 10) });
        vm.SetHurtboxes(new[] { new Rect2(5, 5, 10, 10) });
        Assert.Equal(2, vm.BoxCount);

        vm.Clear();

        Assert.Equal(0, vm.BoxCount);
    }

    [Fact]
    public void Enabled_False_DoesNotAffectBoxStorage()
    {
        var vm = new HitboxOverlayViewModel { Enabled = false };

        vm.SetHitboxes(new[] { new Rect2(0, 0, 10, 10) });

        Assert.Equal(1, vm.BoxCount);
    }
}
