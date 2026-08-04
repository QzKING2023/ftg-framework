using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Core;

public class GameLoopInputRegistrationTests
{
    [Fact]
    public void GameplayActions_AreCanonicalAndPlayerIsolated()
    {
        Assert.Equal(new[]
        {
            "p1_world_left", "p1_world_right", "p1_crouch", "p1_jump", "p1_light",
            "p2_world_left", "p2_world_right", "p2_crouch", "p2_jump", "p2_light"
        }, GameLoop.GameplayActions);
    }

    [Fact]
    public void Defaults_MapNeutralNormals_AndPreserveSpecials()
    {
        var matcher = new InputLeniencyMatcher(new InputHistory(60));
        GameLoop.RegisterDefaultMoves(matcher);
        var moves = matcher.GetRegisteredMoves();

        Assert.Equal(5, moves.Count);
        Assert.Equal(5, moves.Select(move => move.MoveId).Distinct().Count());
        AssertMove("5LP", ButtonValue.A, MoveCategory.Normal, DirectionValue.Neutral);
        AssertMove("5HP", ButtonValue.B, MoveCategory.Normal, DirectionValue.Neutral);
        AssertMove("dp_c", ButtonValue.C, MoveCategory.Special,
            DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward);
        AssertMove("dp_d", ButtonValue.D, MoveCategory.Special,
            DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward);
        AssertMove("fireball_c", ButtonValue.C, MoveCategory.Special,
            DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward);

        void AssertMove(
            string id,
            ButtonValue button,
            MoveCategory category,
            params DirectionValue[] sequence)
        {
            var move = Assert.Single(moves, candidate => candidate.MoveId == id);
            Assert.Equal(button, move.RequiredButton);
            Assert.Equal(category, move.Category);
            Assert.Equal(sequence, Assert.Single(move.AcceptedSequences));
        }
    }

    [Fact]
    public void LocomotionCommand_UsesSocdCleanedVerticalDirection()
    {
        var cleaned = GameLoop.SafeResolve(
            new DefaultSOCDResolver(), left: false, right: false, down: true, up: true);

        var command = GameLoop.BuildLocomotionCommand(1, 0, cleaned, previousJump: false);

        Assert.Equal(DirectionValue.Neutral, cleaned);
        Assert.False(command.CrouchHeld);
        Assert.False(command.JumpPressed);
    }
}
