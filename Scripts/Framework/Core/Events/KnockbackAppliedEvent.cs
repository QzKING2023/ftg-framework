#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct KnockbackAppliedEvent(
    int PlayerId,
    float HorizontalForce,
    float VerticalForce,
    float Gravity,
    float Friction,
    float? WorldX = null,
    float? WorldY = null,
    long GenerationId = 0,
    int ContactFrame = 0,
    int FrameNumber = 0,
    bool Completed = false);
