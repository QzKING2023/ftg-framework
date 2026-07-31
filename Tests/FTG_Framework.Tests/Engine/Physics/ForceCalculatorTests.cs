#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Data;
using FTG_Framework.Engine.Physics;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class ForceCalculatorTests
{
    private static readonly KnockbackProfile Profile = new()
    {
        ProfileId = "test", Horizontal = 10, Vertical = 3, Gravity = 1.5f, Friction = 0.3f
    };

    [Fact]
    public void Launch_ScalesForceAndLaunchesAwayFromAttacker()
    {
        var trajectory = ForceCalculator.Launch(
            generationId: 7, currentX: 5, currentY: 0, attackerX: -5,
            attackerFacingRight: false, new PhysicsMotionSnapshot(0, 0, false, 0),
            Profile, Response(multiplier: 0.3f), contactFrame: 12);

        Assert.Equal(3, trajectory.VelocityX);
        Assert.Equal(-0.9f, trajectory.VelocityY, 5);
        Assert.True(trajectory.Airborne);
        Assert.Equal(7, trajectory.GenerationId);
    }

    [Fact]
    public void Launch_EqualPositions_UsesAttackerFacing()
    {
        var right = ForceCalculator.Launch(
            1, 0, 0, 0, true, new PhysicsMotionSnapshot(0, 0, false, 0),
            Profile, Response(), 1);
        var left = ForceCalculator.Launch(
            2, 0, 0, 0, false, new PhysicsMotionSnapshot(0, 0, false, 0),
            Profile, Response(), 1);

        Assert.Equal(10, right.VelocityX);
        Assert.Equal(-10, left.VelocityX);
    }

    [Fact]
    public void Advance_UsesAirFrictionGravityAndClampsLanding()
    {
        var trajectory = ForceCalculator.Launch(
            1, 5, -1, -5, true, new PhysicsMotionSnapshot(0, 0, true, 0),
            Profile, Response(), 1);

        var result = ForceCalculator.Advance(trajectory, Response());

        Assert.Equal(15, result.PositionX);
        Assert.Equal(-4, result.PositionY);
        Assert.Equal(9.94f, result.VelocityX, 4);
        Assert.Equal(-1.5f, result.VelocityY, 4);
        Assert.False(result.Completed);
    }

    [Fact]
    public void Launch_InvalidValue_FailsWithPhysicsPrefix()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ForceCalculator.Launch(
            1, float.NaN, 0, 0, true, new PhysicsMotionSnapshot(0, 0, false, 0),
            Profile, Response(), 1));
        Assert.StartsWith("[Physics]", ex.Message);
    }

    [Fact]
    public void Launch_SubEpsilonVerticalVelocity_NormalizesToGroundedAtGroundY()
    {
        var trajectory = ForceCalculator.Launch(
            1, 0, 9.9995f, -1, true,
            new PhysicsMotionSnapshot(0, 0, false, 10),
            new KnockbackProfile
            {
                ProfileId = "tiny", Horizontal = 0, Vertical = 0.0005f,
                Gravity = 1, Friction = 0
            },
            Response(), 1);

        Assert.False(trajectory.Airborne);
        Assert.Equal(10, trajectory.PositionY);
        Assert.Equal(0, trajectory.VelocityY);
    }

    [Fact]
    public void Advance_ThreeThousandSixHundredFrames_AllocatesZeroBytes()
    {
        var trajectory = ForceCalculator.Launch(
            1, 0, -100000, -1, true,
            new PhysicsMotionSnapshot(0, 0, true, 0),
            new KnockbackProfile
            {
                ProfileId = "allocation", Horizontal = 1, Vertical = 0,
                Gravity = 0.01f, Friction = 0.01f
            },
            Response(), 1);
        var response = Response();
        for (int i = 0; i < 10; i++)
            trajectory = ForceCalculator.Advance(trajectory, response);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 3_600; i++)
            trajectory = ForceCalculator.Advance(trajectory, response);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static PhysicsResponseProfile Response(float multiplier = 1) => new()
    {
        ProfileId = "effective",
        KnockbackMultiplier = multiplier,
        GravityScale = 1,
        Friction = 0.5f,
        AirFriction = 0.2f
    };
}
