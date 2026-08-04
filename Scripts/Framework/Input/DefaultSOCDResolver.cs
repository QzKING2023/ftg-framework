#nullable enable
using FTG_Framework.Core;

namespace FTG_Framework.Input;

/// <summary>
/// Default modern SOCD-N resolver:
/// L+R → Neutral (both horizontal directions cleared)
/// U+D → Neutral (both vertical directions cleared)
/// All four → Neutral
/// </summary>
internal sealed class DefaultSOCDResolver : ISOCDResolver
{
    public DirectionValue Resolve(bool left, bool right, bool down, bool up)
    {
        // SOCD-N: each opposing pair returns its axis to Neutral.
        bool cleanedUp = up;
        bool cleanedDown = down;
        if (down && up)
        {
            cleanedDown = false;
            cleanedUp = false;
        }

        bool cleanedLeft = left;
        bool cleanedRight = right;
        if (left && right)
        {
            cleanedLeft = false;
            cleanedRight = false;
        }

        return GameLoop.ComputeDirection(cleanedLeft, cleanedRight, cleanedDown, cleanedUp);
    }
}
