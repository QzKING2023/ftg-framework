#nullable enable

namespace FTG_Framework.Core;

public readonly record struct PhysicsFacingSnapshot(
    bool P1FacingRight, bool P2FacingRight, bool BlockEligible);
