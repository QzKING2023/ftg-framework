#nullable enable

namespace FTG_Framework.Data;

public sealed class KnockbackProfile
{
    public string ProfileId { get; init; } = string.Empty;
    public float Horizontal { get; init; }
    public float Vertical { get; init; }
    public float Gravity { get; init; }
    public float Friction { get; init; }
}
