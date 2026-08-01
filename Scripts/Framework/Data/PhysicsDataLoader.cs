#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FTG_Framework.Data;

internal static class PhysicsDataLoader
{
    internal const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static KnockbackProfile[] LoadKnockbackProfilesFromJson(string json)
    {
        ValidateDocument(json, "knockback_profiles",
            ["profile_id", "horizontal", "vertical", "gravity", "friction"],
            booleanField: null);
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
        ValidateDocument(json, "physics_response_profiles",
            ["profile_id", "knockback_multiplier", "gravity_scale", "friction", "air_friction", "participates_in_hitstop"],
            booleanField: "participates_in_hitstop");
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

    private static void ValidateDocument(
        string json,
        string arrayName,
        IReadOnlyCollection<string> requiredFields,
        string? booleanField)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"[Data] Failed to parse JSON: {ex.Message}", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("[Data] Physics document root must be an object.");
            ValidateNoDuplicateProperties(root, "$root");
            if (!root.TryGetProperty("schema_version", out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out int version))
                throw new FormatException("[Data] Required integer schema_version is missing or invalid.");
            if (version != CurrentSchemaVersion)
                throw new FormatException($"[Data] Unsupported schema_version {version}; expected {CurrentSchemaVersion}.");
            if (!root.TryGetProperty(arrayName, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                throw new FormatException($"[Data] JSON must contain a '{arrayName}' array.");

            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                {
                    index++;
                    continue;
                }
                if (item.ValueKind != JsonValueKind.Object)
                    throw new FormatException($"[Data] {arrayName}[{index}] must be an object.");
                foreach (string field in requiredFields)
                {
                    if (!item.TryGetProperty(field, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                        throw new FormatException($"[Data] {arrayName}[{index}] is missing required field '{field}'.");
                    JsonValueKind expected = field == "profile_id"
                        ? JsonValueKind.String
                        : field == booleanField ? JsonValueKind.True : JsonValueKind.Number;
                    bool valid = field == booleanField
                        ? value.ValueKind is JsonValueKind.True or JsonValueKind.False
                        : value.ValueKind == expected;
                    if (!valid)
                        throw new FormatException($"[Data] {arrayName}[{index}].{field} has an invalid JSON type.");
                }
                index++;
            }
        }
    }

    private static void ValidateNoDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new FormatException($"[Data] Duplicate JSON property '{property.Name}' at {path}.");
                ValidateNoDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
                ValidateNoDuplicateProperties(item, $"{path}[{index++}]");
        }
    }

    private sealed class KnockbackProfileListWrapper
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }
        [JsonPropertyName("knockback_profiles")]
        public List<KnockbackProfile> Profiles { get; set; } = new();
    }

    private sealed class PhysicsResponseProfileListWrapper
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }
        [JsonPropertyName("physics_response_profiles")]
        public List<PhysicsResponseProfile> Profiles { get; set; } = new();
    }
}
