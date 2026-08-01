#nullable enable
using System;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public sealed class PlaybackModeCoordinatorTests
{
    [Theory]
    [InlineData(RuntimePlaybackMode.AuthoritativeReplay, RuntimePlaybackMode.TrainingInput)]
    [InlineData(RuntimePlaybackMode.TrainingInput, RuntimePlaybackMode.AuthoritativeReplay)]
    public void Modes_AreMutuallyExclusiveWithinEpoch(RuntimePlaybackMode first, RuntimePlaybackMode second)
    {
        var coordinator = new PlaybackModeCoordinator();
        coordinator.Enter(first, 12);
        Assert.Throws<InvalidOperationException>(() => coordinator.Enter(second, 12));
        Assert.Equal(first, coordinator.ActiveMode);
        Assert.Equal((ulong)12, coordinator.ActiveEpoch);
    }

    [Fact]
    public void Exit_AllowsAnotherModeUnderFreshEpoch()
    {
        var coordinator = new PlaybackModeCoordinator();
        coordinator.Enter(RuntimePlaybackMode.AuthoritativeReplay, 3);
        coordinator.Exit(RuntimePlaybackMode.AuthoritativeReplay);
        coordinator.Enter(RuntimePlaybackMode.TrainingInput, 4);
        Assert.Equal(RuntimePlaybackMode.TrainingInput, coordinator.ActiveMode);
        Assert.Equal((ulong)4, coordinator.ActiveEpoch);
    }
}
