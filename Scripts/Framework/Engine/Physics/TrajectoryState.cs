#nullable enable
using FTG_Framework.Data;

namespace FTG_Framework.Engine.Physics;

internal readonly record struct TrajectoryState(
    long GenerationId,
    float PositionX,
    float PositionY,
    float VelocityX,
    float VelocityY,
    float GroundY,
    bool Airborne,
    KnockbackProfile Profile,
    int ContactFrame,
    bool Completed);
