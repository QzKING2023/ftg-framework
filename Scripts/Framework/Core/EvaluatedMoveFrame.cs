#nullable enable

namespace FTG_Framework.Core;

public readonly record struct EvaluatedMoveFrame(
    string? MoveId, long MoveInstanceId, int TimelineFrame, MovePhase Phase)
{
    public static EvaluatedMoveFrame Idle => new(null, 0, 0, MovePhase.Idle);
    public int AuthoredFrame => TimelineFrame + 1;
}
