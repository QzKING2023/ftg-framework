#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.Core;

public interface IInputLeniency
{
    void RegisterMove(MoveInputConfig config);
    IReadOnlyList<MoveInputConfig> GetRegisteredMoves();
    IReadOnlyList<MatchResult> TryMatch(int playerId);
    IReadOnlyList<MatchResult> TryMatch(int playerId, int fromFrame, int toFrame);
}
