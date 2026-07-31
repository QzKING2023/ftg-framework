#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Data;

public sealed class MoveDefinition
{
    public string MoveId { get; init; } = string.Empty;
    public int Startup { get; init; }
    public int Active { get; init; }
    public int Recovery { get; init; }
    public int HitAdvantage { get; init; }
    public int BlockAdvantage { get; init; }
    public int Damage { get; init; }
    public IReadOnlyList<CancelWindow> CancelWindows { get; init; } = new List<CancelWindow>();
    public bool ChainRepeatable { get; init; }
    public string? KnockbackProfileId { get; init; }
    public string? MoveName { get; init; }
    public IReadOnlyList<CollisionFrameDefinition> CollisionFrames { get; init; } = new List<CollisionFrameDefinition>();
}
