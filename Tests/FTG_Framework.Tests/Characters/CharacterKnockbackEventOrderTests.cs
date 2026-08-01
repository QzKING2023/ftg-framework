#nullable enable
using FTG_Framework.Characters;
using FTG_Framework.Core.Events;
using Xunit;

namespace FTG_Framework.Tests.Characters;

public sealed class CharacterKnockbackEventOrderTests
{
    [Theory]
    [InlineData(2, 10, 1, 99, true)]
    [InlineData(2, 10, 2, 9, true)]
    [InlineData(2, 10, 2, 10, true)]
    [InlineData(1, 100, 2, 10, false)]
    [InlineData(2, 9, 2, 10, false)]
    [InlineData(0, 100, 0, -1, false)]
    public void ShouldApplyKnockbackEvent_RejectsStaleGenerationAndFrame(
        ulong generation, int frame, ulong latestGeneration, int latestFrame, bool expected) =>
        Assert.Equal(expected, CharacterController.ShouldApplyKnockbackEvent(
            generation, frame, latestGeneration, latestFrame));

    [Fact]
    public void ShouldApplyKnockbackEvent_AcceptsNewGenerationStarted()
    {
        var prior = new KnockbackAppliedEvent(
            2, 1, 0, 1, 0.1f, 10, 0, 1, 4, 10, KnockbackPhase.Progressed);
        var next = new KnockbackAppliedEvent(
            2, 2, 0, 1, 0.1f, 11, 0, 2, 5, 11, KnockbackPhase.Started);

        Assert.True(CharacterController.ShouldApplyKnockbackEvent(next, prior));
    }

    [Fact]
    public void ShouldApplyKnockbackEvent_RejectsNewGenerationWithoutStarted()
    {
        var prior = new KnockbackAppliedEvent(
            2, 1, 0, 1, 0.1f, 10, 0, 1, 4, 10, KnockbackPhase.Progressed);
        var next = new KnockbackAppliedEvent(
            2, 2, 0, 1, 0.1f, 11, 0, 2, 5, 11, KnockbackPhase.Progressed);

        Assert.False(CharacterController.ShouldApplyKnockbackEvent(next, prior));
    }

    [Fact]
    public void ShouldApplyKnockbackEvent_EnforcesPhaseOrderDuplicatesAndTerminalState()
    {
        var started = new KnockbackAppliedEvent(
            2, 1, 0, 1, 0.1f, 10, 0, 3, 5, 10, KnockbackPhase.Started);
        var progressed = started with { FrameNumber = 11, Phase = KnockbackPhase.Progressed };
        var completed = progressed with { FrameNumber = 12, Phase = KnockbackPhase.Completed };

        Assert.True(CharacterController.ShouldApplyKnockbackEvent(started, null));
        Assert.False(CharacterController.ShouldApplyKnockbackEvent(started, started));
        Assert.True(CharacterController.ShouldApplyKnockbackEvent(progressed, started));
        Assert.False(CharacterController.ShouldApplyKnockbackEvent(
            progressed with { HorizontalForce = 99 }, progressed));
        Assert.True(CharacterController.ShouldApplyKnockbackEvent(completed, progressed));
        Assert.False(CharacterController.ShouldApplyKnockbackEvent(
            completed with { FrameNumber = 13, Phase = KnockbackPhase.Progressed }, completed));
    }

    [Fact]
    public void ShouldApplyKnockbackEvent_AllowsImmediateCompletionAfterStarted()
    {
        var started = new KnockbackAppliedEvent(
            2, 0, 0, 1, 0.1f, 10, 0, 3, 5, 10, KnockbackPhase.Started);
        var completed = started with { FrameNumber = 11, Phase = KnockbackPhase.Completed };
        Assert.True(CharacterController.ShouldApplyKnockbackEvent(completed, started));
    }

    [Fact]
    public void ShouldApplyKnockbackEvent_RejectsHigherGenerationThatMovesBackwardInTime()
    {
        var prior = new KnockbackAppliedEvent(
            2, 1, 0, 1, 0.1f, 10, 0, 3, 5, 10, KnockbackPhase.Completed);
        var next = prior with
        {
            GenerationId = 4, ContactFrame = 4, FrameNumber = 9, Phase = KnockbackPhase.Started
        };
        Assert.False(CharacterController.ShouldApplyKnockbackEvent(next, prior));
    }
}
