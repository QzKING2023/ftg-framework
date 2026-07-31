#nullable enable
using FTG_Framework.Characters;
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
        long generation, int frame, long latestGeneration, int latestFrame, bool expected) =>
        Assert.Equal(expected, CharacterController.ShouldApplyKnockbackEvent(
            generation, frame, latestGeneration, latestFrame));
}
