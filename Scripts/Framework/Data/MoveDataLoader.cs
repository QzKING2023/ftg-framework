using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using FileAccess = Godot.FileAccess;

namespace FTG_Framework.Data;

internal static class MoveDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static MoveDefinition[] LoadFromFile(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
            throw new FormatException($"[Data] Cannot open file: {path}");
        var json = file.GetAsText();
        return LoadFromJson(json);
    }

    public static MoveDefinition[] LoadFromJson(string json)
    {
        var wrapper = JsonSerializer.Deserialize<MoveListWrapper>(json, JsonOptions);
        if (wrapper?.Moves is null || wrapper.Moves.Count == 0)
            throw new FormatException("[Data] JSON must contain a non-empty 'moves' array.");

        var moves = wrapper.Moves;
        Validate(moves);
        return moves.ToArray();
    }

    private static void Validate(List<MoveDefinition> moves)
    {
        var seenIds = new HashSet<string>();

        foreach (var move in moves)
        {
            if (string.IsNullOrEmpty(move.MoveId))
                throw new FormatException("[Data] Move has null or empty move_id.");

            if (!seenIds.Add(move.MoveId))
                throw new FormatException($"[Data] Duplicate move_id: '{move.MoveId}'.");

            if (move.Startup < 0)
                throw new FormatException($"[Data] Move '{move.MoveId}': startup must be >= 0, got {move.Startup}.");

            if (move.Active < 0)
                throw new FormatException($"[Data] Move '{move.MoveId}': active must be >= 0, got {move.Active}.");

            if (move.Recovery < 0)
                throw new FormatException($"[Data] Move '{move.MoveId}': recovery must be >= 0, got {move.Recovery}.");

            if (move.Damage < 0)
                throw new FormatException($"[Data] Move '{move.MoveId}': damage must be >= 0, got {move.Damage}.");

            if (move.CancelWindows is null)
                throw new FormatException($"[Data] Move '{move.MoveId}': cancel_windows is null. Use [] for no cancel windows or omit the field.");

            foreach (var cw in move.CancelWindows)
            {
                if (cw.StartFrame < 0)
                    throw new FormatException($"[Data] Move '{move.MoveId}': cancel window start_frame must be >= 0.");

                if (cw.EndFrame < cw.StartFrame)
                    throw new FormatException($"[Data] Move '{move.MoveId}': cancel window end_frame ({cw.EndFrame}) < start_frame ({cw.StartFrame}).");

                if (string.IsNullOrEmpty(cw.TargetCategory))
                    throw new FormatException($"[Data] Move '{move.MoveId}': cancel window has null or empty target_category.");
            }
        }
    }

    private sealed class MoveListWrapper
    {
        [JsonPropertyName("moves")]
        public List<MoveDefinition> Moves { get; set; } = new();
    }
}