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
            if (profile is null)
                throw new FormatException("[Data] KnockbackProfile array contains a null entry.");
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] KnockbackProfile has null or empty profile_id.");

            if (!seenIds.Add(profile.ProfileId))
                throw new FormatException($"[Data] Duplicate knockback_profile_id: '{profile.ProfileId}'.");

            ValidateNonNegative(profile.Horizontal, profile.ProfileId, "horizontal", "KnockbackProfile");
            ValidateNonNegative(profile.Vertical, profile.ProfileId, "vertical", "KnockbackProfile");
            ValidateNonNegative(profile.Gravity, profile.ProfileId, "gravity", "KnockbackProfile");
            ValidateNonNegative(profile.Friction, profile.ProfileId, "friction", "KnockbackProfile");
        }
    }

    private static void ValidatePhysicsResponseProfiles(List<PhysicsResponseProfile> profiles)
    {
        var seenIds = new HashSet<string>();

        foreach (var profile in profiles)
        {
            if (profile is null)
                throw new FormatException("[Data] PhysicsResponseProfile array contains a null entry.");
            if (string.IsNullOrEmpty(profile.ProfileId))
                throw new FormatException("[Data] PhysicsResponseProfile has null or empty profile_id.");

            if (!seenIds.Add(profile.ProfileId))
                throw new FormatException($"[Data] Duplicate physics_response_profile_id: '{profile.ProfileId}'.");

            ValidateNonNegative(profile.KnockbackMultiplier, profile.ProfileId, "knockback_multiplier", "PhysicsResponseProfile");
            ValidateNonNegative(profile.GravityScale, profile.ProfileId, "gravity_scale", "PhysicsResponseProfile");
            ValidateNonNegative(profile.Friction, profile.ProfileId, "friction", "PhysicsResponseProfile");
            ValidateNonNegative(profile.AirFriction, profile.ProfileId, "air_friction", "PhysicsResponseProfile");
        }
    }

    private static void ValidateNonNegative(
        float value, string profileId, string field, string kind)
    {
        if (!float.IsFinite(value) || value < 0)
            throw new FormatException(
                $"[Data] {kind} '{profileId}': {field} must be finite and non-negative, got {value}.");
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
