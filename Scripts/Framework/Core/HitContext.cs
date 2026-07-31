#nullable enable
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.Core;

public readonly record struct HitContext(
    HitConnectedEvent Event,
    string HitboxId,
    KnockbackProfile? KnockbackProfile);
