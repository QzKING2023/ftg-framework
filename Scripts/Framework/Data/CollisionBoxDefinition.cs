#nullable enable
namespace FTG_Framework.Data;

public sealed class CollisionBoxDefinition
{
    public string BoxId { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
}
