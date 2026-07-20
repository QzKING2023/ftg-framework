#nullable enable
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests;

public class InputAutoCombineTests
{
    [Fact]
    public void SingleCardinal_ReturnsCorrectDirection()
    {
        Assert.Equal(DirectionValue.Back, GameLoop.ComputeDirection(true, false, false, false));
        Assert.Equal(DirectionValue.Forward, GameLoop.ComputeDirection(false, true, false, false));
        Assert.Equal(DirectionValue.Down, GameLoop.ComputeDirection(false, false, true, false));
        Assert.Equal(DirectionValue.Up, GameLoop.ComputeDirection(false, false, false, true));
    }

    [Fact]
    public void Diagonal_ReturnsCorrectDiagonal()
    {
        Assert.Equal(DirectionValue.DownForward, GameLoop.ComputeDirection(false, true, true, false));
        Assert.Equal(DirectionValue.DownBack, GameLoop.ComputeDirection(true, false, true, false));
        Assert.Equal(DirectionValue.UpForward, GameLoop.ComputeDirection(false, true, false, true));
        Assert.Equal(DirectionValue.UpBack, GameLoop.ComputeDirection(true, false, false, true));
    }

    [Fact]
    public void NoKeys_ReturnsNeutral()
    {
        Assert.Equal(DirectionValue.Neutral, GameLoop.ComputeDirection(false, false, false, false));
    }

    [Fact]
    public void ConflictingKeys_ReturnsNeutral()
    {
        Assert.Equal(DirectionValue.Neutral, GameLoop.ComputeDirection(true, true, false, false));
        Assert.Equal(DirectionValue.Neutral, GameLoop.ComputeDirection(false, false, true, true));
    }
}
