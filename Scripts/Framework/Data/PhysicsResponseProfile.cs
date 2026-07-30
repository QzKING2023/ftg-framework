#nullable enable

namespace FTG_Framework.Data;

public sealed class PhysicsResponseProfile
{
    public string ProfileId { get; init; } = string.Empty;
    public float KnockbackMultiplier { get; init; } = 1.0f;
    public float GravityScale { get; init; } = 1.0f;
    public float Friction { get; init; } = 0.5f;
    public float AirFriction { get; init; } = 0.2f;
    public bool ParticipatesInHitstop { get; init; }
}
