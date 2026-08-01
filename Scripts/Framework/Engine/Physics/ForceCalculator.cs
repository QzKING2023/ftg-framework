#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.Physics;

internal static class ForceCalculator
{
    internal const float CompletionEpsilon = 0.001f;

    internal static TrajectoryState Launch(
        ulong generationId,
        float currentX,
        float currentY,
        float attackerX,
        bool attackerFacingRight,
        in PhysicsMotionSnapshot motion,
        KnockbackProfile profile,
        PhysicsResponseProfile response,
        int contactFrame)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(response);
        ValidateFinite(currentX, nameof(currentX));
        ValidateFinite(currentY, nameof(currentY));
        ValidateFinite(attackerX, nameof(attackerX));
        ValidateMotion(motion);
        ValidateProfile(profile);
        ValidateResponse(response);
        if (motion.GroundY < currentY - CompletionEpsilon)
            Fail("GroundY must be at or below the participant.");

        float direction = currentX > attackerX ? 1f :
            currentX < attackerX ? -1f : attackerFacingRight ? 1f : -1f;
        float vx = direction * profile.Horizontal * response.KnockbackMultiplier;
        float vy = -profile.Vertical * response.KnockbackMultiplier;
        if (motion.Airborne)
        {
            vx += motion.VelocityX;
            vy += motion.VelocityY;
        }
        ValidateFinite(vx, "launch horizontal velocity");
        ValidateFinite(vy, "launch vertical velocity");
        bool airborne = motion.Airborne || vy < -CompletionEpsilon;
        if (!airborne)
        {
            currentY = motion.GroundY;
            vy = 0;
        }
        ValidateCanTerminate(profile, response, airborne, vx);

        return new TrajectoryState(
            generationId, currentX, currentY, vx, vy, motion.GroundY,
            airborne, profile, contactFrame, false);
    }

    internal static TrajectoryState Advance(
        in TrajectoryState trajectory,
        PhysicsResponseProfile response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ValidateResponse(response);
        ValidateCanTerminate(
            trajectory.Profile, response, trajectory.Airborne, trajectory.VelocityX);

        float x = trajectory.PositionX + trajectory.VelocityX;
        float y = trajectory.PositionY + trajectory.VelocityY;
        float selectedFriction = trajectory.Airborne ? response.AirFriction : response.Friction;
        float deceleration = trajectory.Profile.Friction * selectedFriction;
        float vx = MoveTowardZero(trajectory.VelocityX, deceleration);
        float vy = trajectory.VelocityY;
        bool airborne = trajectory.Airborne;
        if (airborne)
        {
            vy += trajectory.Profile.Gravity * response.GravityScale;
            if (vy >= 0 && y >= trajectory.GroundY)
            {
                y = trajectory.GroundY;
                vy = 0;
                airborne = false;
            }
        }
        ValidateFinite(x, "result X");
        ValidateFinite(y, "result Y");
        ValidateFinite(vx, "result horizontal velocity");
        ValidateFinite(vy, "result vertical velocity");
        bool completed = !airborne && MathF.Abs(vx) <= CompletionEpsilon;
        return trajectory with
        {
            PositionX = x,
            PositionY = y,
            VelocityX = vx,
            VelocityY = vy,
            Airborne = airborne,
            Completed = completed
        };
    }

    private static float MoveTowardZero(float value, float amount)
    {
        if (value > 0) return MathF.Max(0, value - amount);
        if (value < 0) return MathF.Min(0, value + amount);
        return 0;
    }

    private static void ValidateMotion(in PhysicsMotionSnapshot motion)
    {
        ValidateFinite(motion.VelocityX, "motion VelocityX");
        ValidateFinite(motion.VelocityY, "motion VelocityY");
        ValidateFinite(motion.GroundY, "motion GroundY");
    }

    private static void ValidateProfile(KnockbackProfile profile)
    {
        ValidateNonNegative(profile.Horizontal, "profile Horizontal");
        ValidateNonNegative(profile.Vertical, "profile Vertical");
        ValidateNonNegative(profile.Gravity, "profile Gravity");
        ValidateNonNegative(profile.Friction, "profile Friction");
    }

    private static void ValidateResponse(PhysicsResponseProfile response)
    {
        ValidateNonNegative(response.KnockbackMultiplier, "response KnockbackMultiplier");
        ValidateNonNegative(response.GravityScale, "response GravityScale");
        ValidateNonNegative(response.Friction, "response Friction");
        ValidateNonNegative(response.AirFriction, "response AirFriction");
    }

    private static void ValidateCanTerminate(
        KnockbackProfile profile, PhysicsResponseProfile response, bool airborne, float vx)
    {
        if (airborne && profile.Gravity * response.GravityScale <= 0)
            Fail("An airborne trajectory requires positive effective gravity.");
        float friction = profile.Friction * (airborne ? response.AirFriction : response.Friction);
        if (MathF.Abs(vx) > CompletionEpsilon && friction <= 0)
            Fail("A horizontal trajectory requires positive effective deceleration.");
    }

    private static void ValidateNonNegative(float value, string name)
    {
        ValidateFinite(value, name);
        if (value < 0) Fail($"{name} must be non-negative.");
    }

    private static void ValidateFinite(float value, string name)
    {
        if (!float.IsFinite(value)) Fail($"{name} must be finite.");
    }

    private static void Fail(string message) =>
        throw new InvalidOperationException($"[Physics] {message}");
}
