#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct MoveStartedEvent(int PlayerId, string MoveId);
