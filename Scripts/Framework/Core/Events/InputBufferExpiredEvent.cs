namespace FTG_Framework.Core.Events;

public readonly record struct InputBufferExpiredEvent(int PlayerId, int Frame);
