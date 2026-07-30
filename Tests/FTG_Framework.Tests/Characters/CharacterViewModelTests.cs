#nullable enable
using FTG_Framework.Characters;
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests.Characters;

public sealed class CharacterViewModelTests
{
    private readonly CharacterViewModel _viewModel = new();

    [Fact]
    public void GetAnimationName_Idle_ReturnsIdle()
    {
        Assert.Equal("idle", _viewModel.GetAnimationName(CharacterState.Idle));
    }

    [Fact]
    public void GetAnimationName_Walk_ReturnsWalk()
    {
        Assert.Equal("walk", _viewModel.GetAnimationName(CharacterState.Walk));
    }

    [Fact]
    public void GetAnimationName_JumpStartup_ReturnsJumpStartup()
    {
        Assert.Equal("jump_startup", _viewModel.GetAnimationName(CharacterState.JumpStartup));
    }

    [Fact]
    public void GetAnimationName_AttackStartup_ReturnsAnimationName()
    {
        Assert.Equal("attack_startup", _viewModel.GetAnimationName(CharacterState.AttackStartup));
        Assert.Equal("attack_active", _viewModel.GetAnimationName(CharacterState.AttackActive));
        Assert.Equal("attack_recovery", _viewModel.GetAnimationName(CharacterState.AttackRecovery));
    }

    [Fact]
    public void GetAnimationName_Hitstun_ReturnsHitstun()
    {
        Assert.Equal("hitstun", _viewModel.GetAnimationName(CharacterState.Hitstun));
    }

    [Fact]
    public void GetAnimationName_UnknownState_ReturnsIdle()
    {
        // Cast an out-of-range value to verify the default fallback
        var unknown = (CharacterState)999;
        Assert.Equal("idle", _viewModel.GetAnimationName(unknown));
    }

    [Fact]
    public void ShouldFaceRight_Forward_ReturnsTrue()
    {
        Assert.True(_viewModel.ShouldFaceRight(DirectionValue.Forward));
        Assert.True(_viewModel.ShouldFaceRight(DirectionValue.UpForward));
        Assert.True(_viewModel.ShouldFaceRight(DirectionValue.DownForward));
    }

    [Fact]
    public void ShouldFaceRight_Back_ReturnsFalse()
    {
        Assert.False(_viewModel.ShouldFaceRight(DirectionValue.Back));
        Assert.False(_viewModel.ShouldFaceRight(DirectionValue.UpBack));
        Assert.False(_viewModel.ShouldFaceRight(DirectionValue.DownBack));
    }

    [Fact]
    public void ShouldFaceRight_NeutralVertical_ReturnsNull()
    {
        Assert.Null(_viewModel.ShouldFaceRight(DirectionValue.Neutral));
        Assert.Null(_viewModel.ShouldFaceRight(DirectionValue.Up));
        Assert.Null(_viewModel.ShouldFaceRight(DirectionValue.Down));
    }

    [Fact]
    public void GetAnimationName_AllStates_HaveMapping()
    {
        foreach (CharacterState state in Enum.GetValues(typeof(CharacterState)))
        {
            var name = _viewModel.GetAnimationName(state);
            Assert.False(string.IsNullOrWhiteSpace(name), $"State {state} has no animation mapping");
        }
    }
}
