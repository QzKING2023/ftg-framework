#nullable enable
using System;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public class PhysicsDataLoaderTests
{
    [Fact]
    public void LoadKnockbackProfiles_ValidJson_ReturnsCorrectProfiles()
    {
        var json = @"{
            ""knockback_profiles"": [
                {
                    ""profile_id"": ""light_hit"",
                    ""horizontal"": 8.0,
                    ""vertical"": 3.0,
                    ""gravity"": 1.5,
                    ""friction"": 0.3
                }
            ]
        }";

        var profiles = PhysicsDataLoader.LoadKnockbackProfilesFromJson(json);

        Assert.NotNull(profiles);
        var profile = Assert.Single(profiles);
        Assert.Equal("light_hit", profile.ProfileId);
        Assert.Equal(8.0f, profile.Horizontal);
        Assert.Equal(3.0f, profile.Vertical);
        Assert.Equal(1.5f, profile.Gravity);
        Assert.Equal(0.3f, profile.Friction);
    }

    [Fact]
    public void LoadPhysicsResponseProfiles_ValidJson_ReturnsCorrectProfiles()
    {
        var json = @"{
            ""physics_response_profiles"": [
                {
                    ""profile_id"": ""hitstun_air"",
                    ""knockback_multiplier"": 0.3,
                    ""gravity_scale"": 1.0,
                    ""friction"": 0.5,
                    ""air_friction"": 0.2,
                    ""participates_in_hitstop"": true
                }
            ]
        }";

        var profiles = PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(json);

        Assert.NotNull(profiles);
        var profile = Assert.Single(profiles);
        Assert.Equal("hitstun_air", profile.ProfileId);
        Assert.Equal(0.3f, profile.KnockbackMultiplier);
        Assert.Equal(1.0f, profile.GravityScale);
        Assert.Equal(0.5f, profile.Friction);
        Assert.Equal(0.2f, profile.AirFriction);
        Assert.True(profile.ParticipatesInHitstop);
    }

    [Fact]
    public void LoadKnockbackProfiles_MultipleProfiles_LoadsAll()
    {
        var json = @"{
            ""knockback_profiles"": [
                { ""profile_id"": ""light"", ""horizontal"": 5.0, ""vertical"": 2.0, ""gravity"": 1.0, ""friction"": 0.3 },
                { ""profile_id"": ""heavy"", ""horizontal"": 12.0, ""vertical"": 5.0, ""gravity"": 2.0, ""friction"": 0.5 }
            ]
        }";

        var profiles = PhysicsDataLoader.LoadKnockbackProfilesFromJson(json);

        Assert.Equal(2, profiles.Length);
        Assert.Equal("light", profiles[0].ProfileId);
        Assert.Equal("heavy", profiles[1].ProfileId);
    }

    [Fact]
    public void LoadKnockbackProfiles_DuplicateProfileId_ThrowsFormatException()
    {
        var json = @"{
            ""knockback_profiles"": [
                { ""profile_id"": ""same_id"", ""horizontal"": 5.0, ""vertical"": 2.0, ""gravity"": 1.0, ""friction"": 0.3 },
                { ""profile_id"": ""same_id"", ""horizontal"": 12.0, ""vertical"": 5.0, ""gravity"": 2.0, ""friction"": 0.5 }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadKnockbackProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
        Assert.Contains("same_id", ex.Message);
    }

    [Fact]
    public void LoadKnockbackProfiles_EmptyArray_ReturnsEmpty()
    {
        var json = @"{ ""knockback_profiles"": [] }";

        var profiles = PhysicsDataLoader.LoadKnockbackProfilesFromJson(json);

        Assert.NotNull(profiles);
        Assert.Empty(profiles);
    }

    [Fact]
    public void LoadKnockbackProfiles_InvalidJson_ThrowsFormatException()
    {
        var json = "not valid json";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadKnockbackProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void LoadKnockbackProfiles_EmptyProfileId_ThrowsFormatException()
    {
        var json = @"{
            ""knockback_profiles"": [
                { ""profile_id"": """", ""horizontal"": 5.0, ""vertical"": 2.0, ""gravity"": 1.0, ""friction"": 0.3 }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadKnockbackProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void LoadKnockbackProfiles_NullProfileId_ThrowsFormatException()
    {
        var json = @"{
            ""knockback_profiles"": [
                { ""horizontal"": 5.0, ""vertical"": 2.0, ""gravity"": 1.0, ""friction"": 0.3 }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadKnockbackProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void LoadKnockbackProfiles_NaN_Horizontal_ThrowsFormatException()
    {
        var json = @"{
            ""knockback_profiles"": [
                { ""profile_id"": ""test"", ""horizontal"": ""NaN"", ""vertical"": 2.0, ""gravity"": 1.0, ""friction"": 0.3 }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadKnockbackProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void LoadPhysicsResponseProfiles_EmptyArray_ReturnsEmpty()
    {
        var json = @"{ ""physics_response_profiles"": [] }";

        var profiles = PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(json);

        Assert.Empty(profiles);
    }

    [Fact]
    public void LoadPhysicsResponseProfiles_NaN_KnockbackMultiplier_ThrowsFormatException()
    {
        var json = @"{
            ""physics_response_profiles"": [
                { ""profile_id"": ""test"", ""knockback_multiplier"": ""NaN"", ""gravity_scale"": 1.0, ""friction"": 0.5, ""air_friction"": 0.2, ""participates_in_hitstop"": false }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void LoadPhysicsResponseProfiles_DuplicateProfileId_ThrowsFormatException()
    {
        var json = @"{
            ""physics_response_profiles"": [
                { ""profile_id"": ""dup"", ""knockback_multiplier"": 1.0, ""gravity_scale"": 1.0, ""friction"": 0.5, ""air_friction"": 0.2, ""participates_in_hitstop"": false },
                { ""profile_id"": ""dup"", ""knockback_multiplier"": 0.5, ""gravity_scale"": 1.0, ""friction"": 0.5, ""air_friction"": 0.2, ""participates_in_hitstop"": false }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() =>
            PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }
}
