#nullable enable
using System;
using System.Text.Json;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Validates replay files before deserialization/playback.
/// </summary>
public static class ReplayVersionValidator
{
    public const int CurrentDataVersion = 1;

    public static void ValidateDeserializedEvent(object? evt, string eventTypeName)
    {
        if (evt is null)
            throw new InvalidOperationException($"[Replay] Deserialized event '{eventTypeName}' is null.");

        var type = evt.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (prop.PropertyType == typeof(string))
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
        if (fileDataVersion != CurrentDataVersion)
            throw new InvalidOperationException(
                $"[Replay] Version mismatch: file v{fileDataVersion}, framework v{CurrentDataVersion}. The replay file was created with a different event schema and cannot be played back.");
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
