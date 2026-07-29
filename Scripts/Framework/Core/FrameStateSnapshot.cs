#nullable enable

namespace FTG_Framework.Core;

public readonly record struct FrameStateSnapshot(
    int Frame,
    string? P1MoveId,
    int P1CurrentFrame,
    MovePhase P1Phase,
    string? P2MoveId,
    int P2CurrentFrame,
    MovePhase P2Phase
);
