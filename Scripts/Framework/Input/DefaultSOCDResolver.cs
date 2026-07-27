#nullable enable
using FTG_Framework.Core;

namespace FTG_Framework.Input;

/// <summary>
/// Default SOCD resolver implementing tournament-standard rules:
/// L+R → Neutral (both horizontal directions cleared)
/// U+D → Up (down cleared, up preserved)
/// All four → Up (U+D resolves first to Up, L+R clears horizontal → Up survives)
/// </summary>
internal sealed class DefaultSOCDResolver : ISOCDResolver
{
    public DirectionValue Resolve(bool left, bool right, bool down, bool up)
    {
        // Step 1: Vertical SOCD — U+D → Up (clear down, keep up)
        bool cleanedUp = up;
        bool cleanedDown = down;
        if (down && up)
        {
            cleanedDown = false;
            // cleanedUp stays true — U+D → Up
        }

        // Step 2: Horizontal SOCD — L+R → Neutral (clear both)
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
