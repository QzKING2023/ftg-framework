#nullable enable
using System.Collections.Generic;
using FTG_Framework.Data;

namespace FTG_Framework.Core;

public readonly record struct PhysicsParticipantSnapshot(
    int PlayerId,
    string CharacterId,
    float WorldX,
    float WorldY,
    DirectionValue Direction,
    bool FacingRight,
    IReadOnlyList<CollisionBoxDefinition> NeutralHurtboxes);
