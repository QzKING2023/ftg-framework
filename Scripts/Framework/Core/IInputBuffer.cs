#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core;

public interface IInputBuffer
{
    int BufferDuration { get; }
    IReadOnlyList<MatchResult> TryMatch(int playerId);
}
