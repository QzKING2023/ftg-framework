#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FTG_Framework.Data;

internal static class PhysicsDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static KnockbackProfile[] LoadKnockbackProfilesFromJson(string json)
    {
        KnockbackProfileListWrapper? wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<KnockbackProfileListWrapper>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"[Data] Failed to parse JSON: {ex.Message}", ex);
        }

        if (wrapper?.Profiles is null)
            throw new FormatException("[Data] JSON must contain a 'knockback_profiles' array.");

        ValidateKnockbackProfiles(wrapper.Profiles);
        return wrapper.Profiles.ToArray();
    }

    public static PhysicsResponseProfile[] LoadPhysicsResponseProfilesFromJson(string json)
    {
        PhysicsResponseProfileListWrapper? wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<PhysicsResponseProfileListWrapper>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"[Data] Failed to parse JSON: {ex.Message}", ex);
        }

        if (wrapper?.Profiles is null)
            throw new FormatException("[Data] JSON must contain a 'physics_response_profiles' array.");

        ValidatePhysicsResponseProfiles(wrapper.Profiles);
        return wrapper.Profiles.ToArray();
    }

    private static void ValidateKnockbackProfiles(List<KnockbackProfile> profiles)
    {
        var seenIds = new HashSet<string>();

        foreach (var profile in profiles)
        {
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] KnockbackProfile has null or empty profile_id.");

            if (!seenIds.Add(profile.ProfileId))
                throw new FormatException($"[Data] Duplicate knockback_profile_id: '{profile.ProfileId}'.");

            if (!float.IsFinite(profile.Horizontal))
                throw new FormatException($"[Data] KnockbackProfile '{profile.ProfileId}': horizontal must be finite, got {profile.Horizontal}.");
            if (!float.IsFinite(profile.Vertical))
                throw new FormatException($"[Data] KnockbackProfile '{profile.ProfileId}': vertical must be finite, got {profile.Vertical}.");
            if (!float.IsFinite(profile.Gravity))
                throw new FormatException($"[Data] KnockbackProfile '{profile.ProfileId}': gravity must be finite, got {profile.Gravity}.");
            if (!float.IsFinite(profile.Friction))
                throw new FormatException($"[Data] KnockbackProfile '{profile.ProfileId}': friction must be finite, got {profile.Friction}.");
        }
    }

    private static void ValidatePhysicsResponseProfiles(List<PhysicsResponseProfile> profiles)
    {
        var seenIds = new HashSet<string>();

        foreach (var profile in profiles)
        {
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] PhysicsResponseProfile has null or empty profile_id.");

            if (!seenIds.Add(profile.ProfileId))
                throw new FormatException($"[Data] Duplicate physics_response_profile_id: '{profile.ProfileId}'.");

            if (!float.IsFinite(profile.KnockbackMultiplier))
                throw new FormatException($"[Data] PhysicsResponseProfile '{profile.ProfileId}': knockback_multiplier must be finite, got {profile.KnockbackMultiplier}.");
            if (!float.IsFinite(profile.GravityScale))
                throw new FormatException($"[Data] PhysicsResponseProfile '{profile.ProfileId}': gravity_scale must be finite, got {profile.GravityScale}.");
            if (!float.IsFinite(profile.Friction))
                throw new FormatException($"[Data] PhysicsResponseProfile '{profile.ProfileId}': friction must be finite, got {profile.Friction}.");
            if (!float.IsFinite(profile.AirFriction))
                throw new FormatException($"[Data] PhysicsResponseProfile '{profile.ProfileId}': air_friction must be finite, got {profile.AirFriction}.");
        }
    }

    private sealed class KnockbackProfileListWrapper
    {
        [JsonPropertyName("knockback_profiles")]
        public List<KnockbackProfile> Profiles { get; set; } = new();
    }

    private sealed class PhysicsResponseProfileListWrapper
    {
        [JsonPropertyName("physics_response_profiles")]
        public List<PhysicsResponseProfile> Profiles { get; set; } = new();
    }
}
