#nullable enable
using FTG_Framework.Core;

namespace FTG_Framework.Characters;

public sealed class CharacterViewModel
{
    public string GetAnimationName(CharacterState state)
    {
        return state switch
        {
            CharacterState.Idle => "idle",
            CharacterState.Walk => "walk",
            CharacterState.Crouch => "crouch",
            CharacterState.JumpStartup => "jump_startup",
            CharacterState.JumpActive => "jump_active",
            CharacterState.JumpRecovery => "jump_recovery",
            CharacterState.AttackStartup => "attack_startup",
            CharacterState.AttackActive => "attack_active",
            CharacterState.AttackRecovery => "attack_recovery",
            CharacterState.Hitstun => "hitstun",
            CharacterState.Blockstun => "blockstun",
            CharacterState.Knockdown => "knockdown",
            CharacterState.Wakeup => "wakeup",
            CharacterState.Airborne => "airborne",
            _ => "idle"
        };
    }

    public bool? ShouldFaceRight(DirectionValue direction)
    {
        return direction switch
        {
            DirectionValue.Forward => true,
            DirectionValue.UpForward => true,
            DirectionValue.DownForward => true,
            DirectionValue.Back => false,
            DirectionValue.UpBack => false,
            DirectionValue.DownBack => false,
            _ => null
        };
    }
}
