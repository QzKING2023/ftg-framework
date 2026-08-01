#nullable enable
using System.Text.Json;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class CollisionReplayCompatibilityTests
{
    [Fact]
    public void CurrentSchemaVersion_IsThree_AndLegacyContainersAreAccepted()
    {
        Assert.Equal(3, ReplayVersionValidator.CurrentDataVersion);
        ReplayVersionValidator.ValidateVersion(1);
        ReplayVersionValidator.ValidateVersion(2);
        ReplayVersionValidator.ValidateVersion(3);
    }

    [Fact]
    public void LegacyHitJson_MissingContactFrame_UsesUnknownSentinel()
    {
        const string json = """{"AttackerId":1,"DefenderId":2,"MoveId":"5A","HitAdvantage":3,"Damage":10}""";
        var value = JsonSerializer.Deserialize<HitConnectedEvent>(json);
        Assert.Equal(-1, value.ContactFrame);
    }

    [Fact]
    public void CurrentHitJson_RoundTripsContactFrame()
    {
        var original = new HitConnectedEvent(1, 2, "5A", 3, 10, 42, "hit-a");
        var restored = JsonSerializer.Deserialize<HitConnectedEvent>(
            JsonSerializer.Serialize(original));
        Assert.Equal(original, restored);
    }

    [Fact]
    public void ReplayPlayback_SkipsLivePhysicsDetection()
    {
        Assert.True(GameLoop.ShouldRunPhysics(replayPlaying: false));
        Assert.False(GameLoop.ShouldRunPhysics(replayPlaying: true));
    }
}
