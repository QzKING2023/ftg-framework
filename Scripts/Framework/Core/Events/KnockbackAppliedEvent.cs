#nullable enable

namespace FTG_Framework.Core.Events;

public enum KnockbackPhase : byte
{
    Started = 1,
    Progressed = 2,
    Completed = 3
}

public readonly record struct KnockbackAppliedEvent(
    int PlayerId,
    float HorizontalForce,
    float VerticalForce,
    float Gravity,
    float Friction,
    float? WorldX = null,
    float? WorldY = null,
    ulong GenerationId = 0,
    int ContactFrame = 0,
    int FrameNumber = 0,
    KnockbackPhase Phase = 0);
