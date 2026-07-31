#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests.Core;

public sealed class GameLoopKnockbackValidationTests
{
    [Fact]
    public void HitCapableMove_WithoutProfile_FailsWithDataPrefix()
    {
        var store = new DataStore([HitMove(null)]);
        var ex = Assert.Throws<FormatException>(
            () => GameLoop.ValidateMoveKnockbackProfiles(store));
        Assert.StartsWith("[Data]", ex.Message);
    }

    [Fact]
    public void HitCapableMove_WithMissingProfile_FailsWithDataPrefix()
    {
        var store = new DataStore([HitMove("missing")]);
        var ex = Assert.Throws<FormatException>(
            () => GameLoop.ValidateMoveKnockbackProfiles(store));
        Assert.Contains("[Data]", ex.Message);
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void HitCapableMove_WithExistingProfile_Passes()
    {
        var store = new DataStore(
            [HitMove("valid")],
            knockbackProfiles: [new KnockbackProfile { ProfileId = "valid" }]);
        GameLoop.ValidateMoveKnockbackProfiles(store);
    }

    [Fact]
    public void MoveWithoutHitboxes_DoesNotRequireProfile()
    {
        var store = new DataStore([new MoveDefinition { MoveId = "movement" }]);
        GameLoop.ValidateMoveKnockbackProfiles(store);
    }

    private static MoveDefinition HitMove(string? profileId) => new()
    {
        MoveId = "hit",
        KnockbackProfileId = profileId,
        CollisionFrames =
        [
            new CollisionFrameDefinition
            {
                Frame = 1,
                Hitboxes = [new CollisionBoxDefinition { BoxId = "hit", Width = 1, Height = 1 }]
            }
        ]
    };
}
