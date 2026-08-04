#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public sealed class ComboDisplayViewModelTests
{
    [Fact]
    public void MissingBucket_IsZeroAndReadDoesNotAllocate()
    {
        var vm = new ComboDisplayViewModel();

        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
        Assert.Equal(0, vm.BucketCount);
    }

    [Fact]
    public void ValidHits_AggregateAuthoritativeDamagePerAttacker()
    {
        var vm = new ComboDisplayViewModel();

        Assert.True(vm.HandleHit(new HitConnectedEvent(1, 2, "5LP", 3, 30, 10, "a")));
        Assert.True(vm.HandleHit(new HitConnectedEvent(2, 1, "5MP", 2, 40, 11, "b")));
        Assert.True(vm.HandleHit(new HitConnectedEvent(1, 2, "5HP", 1, 80, 12, "c")));

        Assert.Equal(new ComboDisplayState(2, 110), vm.GetState(1));
        Assert.Equal(new ComboDisplayState(1, 40), vm.GetState(2));
    }

    [Fact]
    public void BlockAndComboEnd_ResetOnlyReferencedAttacker()
    {
        var vm = CreateTwoAttackers();

        Assert.True(vm.HandleBlock(new MoveBlockedEvent(1, 2, "5HP", -2, 0, 20)));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
        Assert.Equal(new ComboDisplayState(1, 40), vm.GetState(2));

        Assert.True(vm.HandleComboEnded(new ComboEndedEvent(2, 999, "5MP")));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(2));
    }

    public static IEnumerable<object[]> InvalidHits()
    {
        yield return new object[] { new HitConnectedEvent(0, 2, "5LP", 0, 1, 1) };
        yield return new object[] { new HitConnectedEvent(1, 0, "5LP", 0, 1, 1) };
        yield return new object[] { new HitConnectedEvent(1, 1, "5LP", 0, 1, 1) };
        yield return new object[] { new HitConnectedEvent(1, 2, "", 0, 1, 1) };
        yield return new object[] { new HitConnectedEvent(1, 2, "5LP", 0, -1, 1) };
        yield return new object[] { new HitConnectedEvent(1, 2, "5LP", 0, 1) };
        yield return new object[] { default(HitConnectedEvent) };
    }

    [Theory]
    [MemberData(nameof(InvalidHits))]
    public void InvalidHit_RejectsWithoutMutatingAnyBucket(HitConnectedEvent invalid)
    {
        var diagnostics = new List<string>();
        var vm = CreateTwoAttackers(diagnostics.Add);
        var before1 = vm.GetState(1);
        var before2 = vm.GetState(2);

        Assert.False(vm.HandleHit(invalid));

        Assert.Equal(before1, vm.GetState(1));
        Assert.Equal(before2, vm.GetState(2));
        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData(int.MaxValue, 0, 1)]
    [InlineData(1, int.MaxValue, 1)]
    public void CheckedAccumulation_RejectsWholeHitAndPreservesPriorState(
        int hitCount, int totalDamage, int incomingDamage)
    {
        var diagnostics = new List<string>();
        var prior = new ComboDisplayState(hitCount, totalDamage);

        Assert.False(ComboDisplayViewModel.TryAccumulate(
            1, prior, incomingDamage, diagnostics.Add, out var result));

        Assert.Equal(prior, result);
        Assert.Single(diagnostics);
        Assert.Contains("attacker 1", diagnostics[0]);
    }

    [Fact]
    public void ResetAll_ClearsEveryBucket()
    {
        var vm = CreateTwoAttackers();

        vm.ResetAll();

        Assert.Equal(ComboDisplayState.Empty, vm.GetState(1));
        Assert.Equal(ComboDisplayState.Empty, vm.GetState(2));
        Assert.Equal(0, vm.BucketCount);
    }

    private static ComboDisplayViewModel CreateTwoAttackers(System.Action<string>? diagnostic = null)
    {
        var vm = new ComboDisplayViewModel(diagnostic);
        Assert.True(vm.HandleHit(new HitConnectedEvent(1, 2, "5LP", 3, 30, 10, "a")));
        Assert.True(vm.HandleHit(new HitConnectedEvent(2, 1, "5MP", 2, 40, 11, "b")));
        return vm;
    }
}
