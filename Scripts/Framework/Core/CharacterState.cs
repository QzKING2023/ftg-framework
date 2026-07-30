#nullable enable

namespace FTG_Framework.Core;

public enum CharacterState
{
    Idle,
    Walk,
    Crouch,
    JumpStartup,
    JumpActive,
    JumpRecovery,
    AttackStartup,
    AttackActive,
    AttackRecovery,
    Hitstun,
    Blockstun,
    Knockdown,
    Wakeup,
    Airborne
}
