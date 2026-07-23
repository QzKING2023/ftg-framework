namespace FTG_Framework.Core.Events;

public readonly record struct MoveBlockedEvent(int AttackerId, int DefenderId, string MoveId, int BlockAdvantage, int Damage);
