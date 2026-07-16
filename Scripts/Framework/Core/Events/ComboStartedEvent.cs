namespace FTG_Framework.Core.Events;

public readonly record struct ComboStartedEvent(int PlayerId, string MoveId, int HitCount);
