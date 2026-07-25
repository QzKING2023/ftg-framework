#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct CancelWindowExitedEvent(int PlayerId, string MoveId, string Category);
