namespace FTG_Framework.Core.Events;

public readonly record struct HitConnectedEvent(int AttackerId, int DefenderId, string MoveId, int HitAdvantage);
