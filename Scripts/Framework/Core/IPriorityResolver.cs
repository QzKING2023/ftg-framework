#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core;

public interface IPriorityResolver
{
    MatchResult? Resolve(IReadOnlyList<MatchResult> candidates, int playerId, int currentFrame);
}
