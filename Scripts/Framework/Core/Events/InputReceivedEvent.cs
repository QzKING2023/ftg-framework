namespace FTG_Framework.Core.Events;

public readonly record struct InputReceivedEvent(int PlayerId, int Frame, int InputType, int InputValue);
