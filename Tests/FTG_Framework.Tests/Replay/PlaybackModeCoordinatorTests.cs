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

    [Fact]
    public void Capture_ExcludesAuthoritativeReplayButAllowsTrainingInputForOtherPlayer()
    {
        var coordinator = new PlaybackModeCoordinator();
        Assert.True(coordinator.TryEnterCapture(1, 7, out _));
        coordinator.Enter(RuntimePlaybackMode.TrainingInput, 7);
        coordinator.Exit(RuntimePlaybackMode.TrainingInput);

        Assert.Throws<InvalidOperationException>(() =>
            coordinator.Enter(RuntimePlaybackMode.AuthoritativeReplay, 7));
        Assert.Equal(1, coordinator.CapturePlayer);
    }

    [Fact]
    public void TrainingInput_RejectsDifferentEpochWhenCaptureAlreadyOwnsLifecycle()
    {
        var coordinator = new PlaybackModeCoordinator();
        Assert.True(coordinator.TryEnterCapture(1, 7, out _));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            coordinator.Enter(RuntimePlaybackMode.TrainingInput, 8));

        Assert.Contains("epoch", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RuntimePlaybackMode.None, coordinator.ActiveMode);
        Assert.Equal((ulong)0, coordinator.ActiveEpoch);
        Assert.Equal(1, coordinator.CapturePlayer);
        Assert.Equal((ulong)7, coordinator.CaptureEpoch);
    }

    [Fact]
    public void Capture_RejectsDifferentEpochWhenTrainingInputAlreadyOwnsLifecycle()
    {
        var coordinator = new PlaybackModeCoordinator();
        coordinator.Enter(RuntimePlaybackMode.TrainingInput, 8);

        Assert.False(coordinator.TryEnterCapture(1, 7, out string error));

        Assert.Contains("epoch", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RuntimePlaybackMode.TrainingInput, coordinator.ActiveMode);
        Assert.Equal((ulong)8, coordinator.ActiveEpoch);
        Assert.Equal(0, coordinator.CapturePlayer);
        Assert.Equal((ulong)0, coordinator.CaptureEpoch);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaptureAndTrainingInput_ShareSameEpochInEitherAcquisitionOrder(bool captureFirst)
    {
        var coordinator = new PlaybackModeCoordinator();
        if (captureFirst)
        {
            Assert.True(coordinator.TryEnterCapture(1, 7, out _));
            coordinator.Enter(RuntimePlaybackMode.TrainingInput, 7);
        }
        else
        {
            coordinator.Enter(RuntimePlaybackMode.TrainingInput, 7);
            Assert.True(coordinator.TryEnterCapture(1, 7, out _));
        }

        Assert.Equal(RuntimePlaybackMode.TrainingInput, coordinator.ActiveMode);
        Assert.Equal((ulong)7, coordinator.ActiveEpoch);
        Assert.Equal(1, coordinator.CapturePlayer);
        Assert.Equal((ulong)7, coordinator.CaptureEpoch);
    }
}
