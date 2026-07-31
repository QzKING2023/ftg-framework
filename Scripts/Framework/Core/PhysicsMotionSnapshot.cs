#nullable enable

namespace FTG_Framework.Core;

public readonly record struct PhysicsMotionSnapshot(
    float VelocityX,
    float VelocityY,
    bool Airborne,
    float GroundY);
