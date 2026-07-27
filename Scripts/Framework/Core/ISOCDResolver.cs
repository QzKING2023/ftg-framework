#nullable enable
namespace FTG_Framework.Core;

/// <summary>
/// Resolves simultaneous opposite cardinal directions (SOCD) from raw boolean key states.
/// Implementations must handle all 16 possible input combinations.
/// The default resolution rules are: L+R → Neutral, U+D → Up.
/// When all four directions are pressed, resolution order is: (1) U+D → Up, (2) L+R → Neutral — final output: Up.
/// </summary>
public interface ISOCDResolver
{
    DirectionValue Resolve(bool left, bool right, bool down, bool up);
}
