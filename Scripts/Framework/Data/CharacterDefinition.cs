#nullable enable
using System.Collections.Generic;
namespace FTG_Framework.Data;

public sealed class CharacterDefinition
{
    public string CharacterId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ScenePath { get; init; } = string.Empty;
    public IReadOnlyList<CollisionBoxDefinition> NeutralHurtboxes { get; init; } = new List<CollisionBoxDefinition>();
}
