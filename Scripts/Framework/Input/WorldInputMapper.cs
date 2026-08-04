#nullable enable
using System;
using FTG_Framework.Core;

namespace FTG_Framework.Input;

public enum AuthoritativeFacing { Left = -1, Right = 1 }
public readonly record struct WorldInputSample(int HorizontalAxis, bool Crouch, bool Jump);
public readonly record struct CanonicalInputSample(DirectionValue Direction, bool IsValid);
public readonly record struct FacingPair(AuthoritativeFacing P1, AuthoritativeFacing P2, bool BlockEligible);

public static class WorldInputMapper
{
    public static CanonicalInputSample Map(in WorldInputSample sample, AuthoritativeFacing facing)
    {
        if (facing is not AuthoritativeFacing.Left and not AuthoritativeFacing.Right || sample.HorizontalAxis is < -1 or > 1)
        {
            FrameworkLog.Error?.Invoke("[Input] Invalid world input axis or authoritative facing; using Neutral.");
            return new CanonicalInputSample(DirectionValue.Neutral, false);
        }
        bool forward = sample.HorizontalAxis != 0 && (sample.HorizontalAxis > 0) == (facing == AuthoritativeFacing.Right);
        bool back = sample.HorizontalAxis != 0 && !forward;
        return new CanonicalInputSample(GameLoop.ComputeDirection(back, forward, sample.Crouch, sample.Jump), true);
    }
}

public static class FacingResolver
{
    public static FacingPair Resolve(float p1X, float p2X, AuthoritativeFacing priorP1, AuthoritativeFacing priorP2)
    {
        if (!float.IsFinite(p1X) || !float.IsFinite(p2X))
            throw new ArgumentOutOfRangeException(nameof(p1X), "[Input] World positions must be finite.");
        if (priorP1 is not AuthoritativeFacing.Left and not AuthoritativeFacing.Right || priorP2 is not AuthoritativeFacing.Left and not AuthoritativeFacing.Right)
            throw new ArgumentOutOfRangeException(nameof(priorP1), "[Input] Prior facing must be Left or Right.");
        if (p1X < p2X) return new FacingPair(AuthoritativeFacing.Right, AuthoritativeFacing.Left, true);
        if (p1X > p2X) return new FacingPair(AuthoritativeFacing.Left, AuthoritativeFacing.Right, true);
        return new FacingPair(priorP1, priorP2, false);
    }
}
