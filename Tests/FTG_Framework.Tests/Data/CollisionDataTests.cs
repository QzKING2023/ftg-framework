#nullable enable
using System;
using System.Linq;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class CollisionDataTests
{
    [Fact]
    public void MoveLoader_ParsesPerFrameHitboxesAndHurtboxes()
    {
        const string json = """
        {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":0,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],
        "collision_frames":[{"frame":2,
          "hitboxes":[{"box_id":"hit-1","x":10,"y":0,"width":20,"height":10}],
          "hurtboxes":[{"box_id":"body","x":0,"y":0,"width":16,"height":40}]}]}]}
        """;

        var move = MoveDataLoader.LoadFromJson(json).Single();

        var frame = Assert.Single(move.CollisionFrames);
        Assert.Equal(2, frame.Frame);
        Assert.Equal("hit-1", Assert.Single(frame.Hitboxes).BoxId);
        Assert.Equal("body", Assert.Single(frame.Hurtboxes).BoxId);
    }

    [Fact]
    public void MoveLoader_OmittedCollisionFrames_IsRejected()
    {
        const string json = """{"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":0,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[]}]}""";
        var ex = Assert.ThrowsAny<FormatException>(() => MoveDataLoader.LoadFromJson(json));
        Assert.Contains("collision_frames", ex.Message);
    }

    [Theory]
    [InlineData("""{"frame":0,"hitboxes":[],"hurtboxes":[]}""")]
    [InlineData("""{"frame":1,"hitboxes":[{"box_id":"x","x":0,"y":0,"width":-1,"height":1}],"hurtboxes":[]}""")]
    [InlineData("""{"frame":1,"hitboxes":[{"box_id":"x","x":0,"y":0,"width":"NaN","height":1}],"hurtboxes":[]}""")]
    public void MoveLoader_InvalidCollisionData_FailsFast(string frameJson)
    {
        var json = $$"""{"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":0,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[{{frameJson}}]}]}""";
        var ex = Assert.ThrowsAny<Exception>(() => MoveDataLoader.LoadFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void HitEvents_FiveArgumentConstruction_UsesUnknownContactFrame()
    {
        var hit = new HitConnectedEvent(1, 2, "5A", 3, 10);
        var block = new MoveBlockedEvent(1, 2, "5A", -2, 10);
        Assert.Equal(-1, hit.ContactFrame);
        Assert.Equal(-1, block.ContactFrame);
    }

    [Fact]
    public void CharacterLoader_ParsesNeutralHurtboxes()
    {
        const string json = """
        [{"characterId":"fighter","displayName":"Fighter","scenePath":"res://fighter.tscn",
          "neutralHurtboxes":[{"boxId":"body","x":0,"y":-10,"width":20,"height":40}]}]
        """;
        var character = Assert.Single(CharacterDataLoader.LoadFromJson(json));
        Assert.Equal("body", Assert.Single(character.NeutralHurtboxes).BoxId);
    }

    [Theory]
    [InlineData("""{"schema_version":1,"moves":[null]}""")]
    [InlineData("""{"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":0,"block_advantage":0,"damage":0,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[null]}]}""")]
    [InlineData("""{"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":0,"block_advantage":0,"damage":0,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[{"frame":1,"hitboxes":[null],"hurtboxes":[]}]}]}""")]
    public void MoveLoader_NullCollisionElements_FailWithDataError(string json)
    {
        var ex = Assert.ThrowsAny<FormatException>(() => MoveDataLoader.LoadFromJson(json));
        Assert.StartsWith("[Data]", ex.Message);
    }

    [Fact]
    public void MoveLoader_DurationOverflow_FailsWithDataError()
    {
        const string json = """
        {"schema_version":1,"moves":[{"move_id":"huge","startup":2147483647,"active":2147483647,
        "recovery":2147483647,"hit_advantage":0,"block_advantage":0,"damage":0,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[{"frame":1,"hitboxes":[],"hurtboxes":[]}]}]}
        """;
        var ex = Assert.ThrowsAny<FormatException>(() => MoveDataLoader.LoadFromJson(json));
        Assert.StartsWith("[Data]", ex.Message);
    }

    [Theory]
    [InlineData("""[null]""")]
    [InlineData("""[{"characterId":"fighter","displayName":"Fighter","neutralHurtboxes":[null]}]""")]
    [InlineData("""[{"characterId":"fighter","displayName":"Fighter","neutralHurtboxes":[{"boxId":"body","x":1e400,"width":1,"height":1}]}]""")]
    public void CharacterLoader_InvalidJsonAndNullElements_FailWithDataError(string json)
    {
        var ex = Assert.Throws<FormatException>(() => CharacterDataLoader.LoadFromJson(json));
        Assert.StartsWith("[Data]", ex.Message);
    }
}
