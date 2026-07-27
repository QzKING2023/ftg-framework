#nullable enable
namespace FTG_Framework.Core.Events;

public readonly record struct CharacterSelectedEvent(int PlayerSlot, string CharacterId);
