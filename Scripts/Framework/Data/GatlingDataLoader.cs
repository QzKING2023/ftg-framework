#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using FileAccess = Godot.FileAccess;

namespace FTG_Framework.Data;

internal static class GatlingDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static GatlingTable[] LoadFromFile(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
            throw new FormatException($"[Data] Cannot open file: {path}");
        var json = file.GetAsText();
        return LoadFromJson(json);
    }

    public static GatlingTable[] LoadFromJson(string json)
    {
        GatlingTableListWrapper? wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<GatlingTableListWrapper>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"[Data] Failed to parse JSON: {ex.Message}", ex);
        }

        if (wrapper?.Tables is null || wrapper.Tables.Count == 0)
            throw new FormatException("[Data] JSON must contain a non-empty 'gatling_tables' array.");

        var tables = wrapper.Tables;
        Validate(tables);
        return tables.ToArray();
    }

    private static void Validate(List<GatlingTable> tables)
    {
        var seenIds = new HashSet<string>();

        foreach (var table in tables)
        {
            if (string.IsNullOrEmpty(table.CharacterId))
                throw new FormatException("[Data] Gatling table has null or empty character_id.");

            if (!seenIds.Add(table.CharacterId))
                throw new FormatException($"[Data] Duplicate gatling table for character_id: '{table.CharacterId}'.");

            if (table.Entries is null)
                throw new FormatException($"[Data] Gatling table '{table.CharacterId}': entries is null. Use [] for no entries.");

            var seenEntries = new HashSet<string>();
            foreach (var entry in table.Entries)
            {
                if (string.IsNullOrEmpty(entry.SourceMove))
                    throw new FormatException($"[Data] Gatling entry in table '{table.CharacterId}' has null or empty source_move.");

                if (entry.TargetMoves is null || entry.TargetMoves.Count == 0)
                    throw new FormatException($"[Data] Gatling entry for source_move '{entry.SourceMove}' in table '{table.CharacterId}' has null or empty target_moves.");

                if (string.IsNullOrEmpty(entry.CancelCategory))
                    throw new FormatException($"[Data] Gatling entry for source_move '{entry.SourceMove}' in table '{table.CharacterId}' has null or empty cancel_category.");

                foreach (var target in entry.TargetMoves)
                {
                    if (string.IsNullOrEmpty(target))
                        throw new FormatException($"[Data] Gatling entry for source_move '{entry.SourceMove}' in table '{table.CharacterId}' contains null or empty target move.");
                }

                // Duplicate check: same source_move + cancel_category + any overlapping target_move
                foreach (var target in entry.TargetMoves)
                {
                    var key = $"{entry.SourceMove}|{target}|{entry.CancelCategory}";
                    if (!seenEntries.Add(key))
                        throw new FormatException($"[Data] Gatling table '{table.CharacterId}': duplicate entry (source_move='{entry.SourceMove}', target_move='{target}', cancel_category='{entry.CancelCategory}').");
                }
            }
        }
    }

    private sealed class GatlingTableListWrapper
    {
        [JsonPropertyName("gatling_tables")]
        public List<GatlingTable> Tables { get; set; } = new();
    }
}
