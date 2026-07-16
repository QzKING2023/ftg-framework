namespace FTG_Framework.Core.Events;

public readonly record struct CancelWindowEnteredEvent(int PlayerId, string MoveId, string Category, int StartFrame, int EndFrame);
