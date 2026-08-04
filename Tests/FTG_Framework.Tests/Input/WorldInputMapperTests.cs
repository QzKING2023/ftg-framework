using System;
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Input;

public sealed class WorldInputMapperTests
{
    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.PositiveInfinity)]
    public void FacingResolver_NonFinitePosition_Throws(float p1X, float p2X)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FacingResolver.Resolve(
            p1X, p2X, AuthoritativeFacing.Right, AuthoritativeFacing.Left));
    }

    [Theory]
    [InlineData(-1, AuthoritativeFacing.Right, DirectionValue.Back)]
    [InlineData(1, AuthoritativeFacing.Right, DirectionValue.Forward)]
    [InlineData(-1, AuthoritativeFacing.Left, DirectionValue.Forward)]
    [InlineData(1, AuthoritativeFacing.Left, DirectionValue.Back)]
    public void Map_ConvertsWorldAxisAgainstFacing(int worldAxis, AuthoritativeFacing facing, DirectionValue expected)
    {
        var result = WorldInputMapper.Map(new WorldInputSample(worldAxis, false, false), facing);
        Assert.Equal(expected, result.Direction);
    }

    [Fact]
    public void Map_CombinesCrouchAndFacingRelativeAxis()
    {
        var result = WorldInputMapper.Map(new WorldInputSample(-1, true, false), AuthoritativeFacing.Left);
        Assert.Equal(DirectionValue.DownForward, result.Direction);
    }

    [Fact]
    public void Map_InvalidFacing_IsRejectedToNeutral()
    {
        var result = WorldInputMapper.Map(new WorldInputSample(1, false, false), (AuthoritativeFacing)0);
        Assert.False(result.IsValid);
        Assert.Equal(DirectionValue.Neutral, result.Direction);
    }

    [Fact]
    public void FacingResolver_PreservesPriorFacingAtEqualX()
    {
        var resolved = FacingResolver.Resolve(10, 10, AuthoritativeFacing.Left, AuthoritativeFacing.Right);
        Assert.Equal(AuthoritativeFacing.Left, resolved.P1);
        Assert.Equal(AuthoritativeFacing.Right, resolved.P2);
        Assert.False(resolved.BlockEligible);
    }

    [Fact]
    public void FacingResolver_UsesOpponentPositions_AndHandlesCrossover()
    {
        var before = FacingResolver.Resolve(0, 20, AuthoritativeFacing.Right, AuthoritativeFacing.Left);
        var after = FacingResolver.Resolve(21, 20, before.P1, before.P2);
        Assert.Equal(AuthoritativeFacing.Right, before.P1);
        Assert.Equal(AuthoritativeFacing.Left, before.P2);
        Assert.Equal(AuthoritativeFacing.Left, after.P1);
        Assert.Equal(AuthoritativeFacing.Right, after.P2);
    }
}
