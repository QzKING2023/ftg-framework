#nullable enable

namespace FTG_Framework.Core;

public readonly record struct PhysicsConsumedContact(
    long MoveInstanceId, int AttackerId, int DefenderId);
