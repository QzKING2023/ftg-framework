#nullable enable
namespace FTG_Framework.Core.Events;

public readonly record struct MatchInitializedEvent(string P1CharacterId, string P2CharacterId);
