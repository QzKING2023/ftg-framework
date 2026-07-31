#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Data;

public sealed class CollisionFrameDefinition
{
    public int Frame { get; init; }
    public IReadOnlyList<CollisionBoxDefinition> Hitboxes { get; init; } = new List<CollisionBoxDefinition>();
    public IReadOnlyList<CollisionBoxDefinition> Hurtboxes { get; init; } = new List<CollisionBoxDefinition>();
}
