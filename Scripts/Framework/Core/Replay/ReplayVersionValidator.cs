#nullable enable
using System;
using System.Reflection;
using System.Text.Json;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Validates replay files before deserialization/playback.
/// </summary>
public static class ReplayVersionValidator
{
    private static readonly JsonSerializerOptions ReplayJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public const int CurrentDataVersion = 3;
    public const int OldestSupportedDataVersion = 1;

    public static void ValidateDeserializedEvent(object? evt, string eventTypeName)
    {
        if (evt is null)
            throw new InvalidOperationException($"[Replay] Deserialized event '{eventTypeName}' is null.");

        var type = evt.GetType();
        var nullability = new NullabilityInfoContext();
        foreach (var prop in type.GetProperties())
        {
            if (prop.PropertyType == typeof(string) &&
                nullability.Create(prop).ReadState == NullabilityState.NotNull)
            {
                var value = prop.GetValue(evt) as string;
                if (value is null)
                    throw new InvalidOperationException(
                        $"[Replay] Deserialized event '{eventTypeName}' has null string field '{prop.Name}'. Replay file may be corrupted.");
            }
        }
    }

    /// <summary>
    /// Validates version compatibility. Throws on mismatch.
    /// </summary>
    public static void ValidateVersion(int fileDataVersion)
    {
        if (fileDataVersion < OldestSupportedDataVersion || fileDataVersion > CurrentDataVersion)
            throw new InvalidOperationException(
                $"[Replay] Version mismatch: file v{fileDataVersion}, framework v{CurrentDataVersion}. The replay file was created with a different event schema and cannot be played back.");
    }

    /// <summary>
    /// Validates version-sensitive payload shape before a replay is accepted.
    /// Legacy knockback payloads used a Completed Boolean and cannot be inferred
    /// safely now that the lifecycle has three explicit phases.
    /// </summary>
    internal static void ValidateEntryPayload(int fileDataVersion, ReplayEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            if (string.Equals(entry.EventType, "KnockbackAppliedEvent", StringComparison.Ordinal))
            {
                using var document = JsonDocument.Parse(entry.Payload);
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !TryGetPropertyIgnoreCase(document.RootElement, "Phase", out var phase) ||
                    phase.ValueKind != JsonValueKind.Number || !phase.TryGetByte(out byte value) ||
                    value is < 1 or > 3)
                {
                    throw new InvalidOperationException(
                        $"[Replay] KnockbackAppliedEvent in data version {fileDataVersion} requires numeric Phase 1 (Started), 2 (Progressed), or 3 (Completed).");
                }
            }

            var eventType = EventTypeRegistry.Resolve(entry.EventType);
            if (eventType is null)
                return;
            var deserialized = JsonSerializer.Deserialize(entry.Payload, eventType, ReplayJsonOptions);
            ValidateDeserializedEvent(deserialized, entry.EventType);
            if (deserialized is KnockbackAppliedEvent knockback &&
                (knockback.PlayerId is < 1 or > 2 || knockback.GenerationId == 0 ||
                 knockback.Phase is < KnockbackPhase.Started or > KnockbackPhase.Completed ||
                 !knockback.WorldX.HasValue || !knockback.WorldY.HasValue ||
                 !float.IsFinite(knockback.HorizontalForce) || !float.IsFinite(knockback.VerticalForce) ||
                 !float.IsFinite(knockback.Gravity) || !float.IsFinite(knockback.Friction) ||
                 !float.IsFinite(knockback.WorldX.Value) || !float.IsFinite(knockback.WorldY.Value)))
            {
                throw new InvalidOperationException(
                    $"[Replay] KnockbackAppliedEvent in data version {fileDataVersion} contains invalid required fields.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"[Replay] Invalid KnockbackAppliedEvent payload in data version {fileDataVersion}: {ex.Message}", ex);
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Validates framework version compatibility (major.minor must match).
    /// </summary>
    public static void ValidateFrameworkVersion(string fileFrameworkVersion, string currentFrameworkVersion)
    {
        var fileParts = fileFrameworkVersion.Split('.');
        var currentParts = currentFrameworkVersion.Split('.');
        if (fileParts.Length < 2 || currentParts.Length < 2)
            throw new InvalidOperationException(
                $"[Replay] Invalid framework version format: file='{fileFrameworkVersion}', framework='{currentFrameworkVersion}'.");

        if (fileParts[0] != currentParts[0] || fileParts[1] != currentParts[1])
            throw new InvalidOperationException(
                $"[Replay] Framework version mismatch: file v{fileFrameworkVersion}, framework v{currentFrameworkVersion}. Major.minor must match for replay compatibility.");
    }
}
