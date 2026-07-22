#nullable enable
using FTG_Framework.Core;

namespace FTG_Framework.Engine.FrameData;

internal readonly record struct FrameStateSnapshot(
    int Frame,
    string? P1MoveId,
    int P1CurrentFrame,
    MovePhase P1Phase,
    string? P2MoveId,
    int P2CurrentFrame,
    MovePhase P2Phase
);
