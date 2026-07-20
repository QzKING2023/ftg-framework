namespace FTG_Framework.Core.Events;

public readonly record struct MoveFrameChangedEvent(int PlayerId, string MoveId, int CurrentFrame, int TotalFrames, MovePhase Phase);
