#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace FTG_Framework.Data;

internal static class CharacterDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static CharacterDefinition[] LoadFromFile(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
            throw new FormatException($"[Data] Cannot open file: {path}");
        var json = file.GetAsText();
        return LoadFromJson(json);
    }

    public static CharacterDefinition[] LoadFromJson(string json)
    {
        var characters = JsonSerializer.Deserialize<CharacterDefinition[]>(json, JsonOptions);
        if (characters is null || characters.Length == 0)
            throw new FormatException("[Data] JSON must contain a non-empty character array.");

        Validate(characters);
        return characters;
    }

    private static void Validate(CharacterDefinition[] characters)
    {
        var seenIds = new HashSet<string>();

        foreach (var c in characters)
        {
            if (string.IsNullOrEmpty(c.CharacterId))
                throw new FormatException("[Data] Character has null or empty CharacterId.");

            if (!seenIds.Add(c.CharacterId))
                throw new FormatException($"[Data] Duplicate CharacterId: '{c.CharacterId}'.");

            if (string.IsNullOrEmpty(c.DisplayName))
                throw new FormatException($"[Data] Character '{c.CharacterId}': display_name is null or empty.");
        }
    }
}
