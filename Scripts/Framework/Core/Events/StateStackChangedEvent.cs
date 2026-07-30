#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct StateStackChangedEvent(int PlayerId, CharacterState[] Stack);
