#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct ChargeStateChangedEvent(int PlayerId, int Direction, bool IsCharged);
