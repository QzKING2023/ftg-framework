#nullable enable

namespace FTG_Framework.Core.Events;

public readonly record struct StateChangedEvent(int PlayerId, CharacterState[] OldStack, CharacterState[] NewStack);
