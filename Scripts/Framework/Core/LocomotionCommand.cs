#nullable enable

namespace FTG_Framework.Core;

public readonly record struct LocomotionCommand(
    int PlayerId, int WorldAxis, bool CrouchHeld, bool JumpPressed);
