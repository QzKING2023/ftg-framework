#nullable enable
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
        MoveListWrapper? wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<MoveListWrapper>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"[Data] Invalid move JSON: {ex.Message}", ex);
        }
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
            if (move is null)
                throw new FormatException("[Data] Moves array cannot contain null entries.");
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

            if (move.CollisionFrames is null)
                throw new FormatException($"[Data] Move '{move.MoveId}': collision_frames is null. Use [] or omit the field.");

            var seenFrames = new HashSet<int>();
            long totalFrames = (long)move.Startup + move.Active + move.Recovery;
            if (totalFrames > int.MaxValue)
                throw new FormatException(
                    $"[Data] Move '{move.MoveId}': total duration exceeds {int.MaxValue} frames.");
            foreach (var frame in move.CollisionFrames)
            {
                if (frame is null)
                    throw new FormatException($"[Data] Move '{move.MoveId}': collision_frames cannot contain null entries.");
                if (frame.Frame < 1 || frame.Frame > totalFrames)
                    throw new FormatException($"[Data] Move '{move.MoveId}': collision frame {frame.Frame} must be in 1..{totalFrames}.");
                if (!seenFrames.Add(frame.Frame))
                    throw new FormatException($"[Data] Move '{move.MoveId}': duplicate collision frame {frame.Frame}.");
                if (frame.Hitboxes is null || frame.Hurtboxes is null)
                    throw new FormatException($"[Data] Move '{move.MoveId}' frame {frame.Frame}: hitboxes/hurtboxes cannot be null.");
                ValidateBoxes(move.MoveId, frame.Frame, "hitbox", frame.Hitboxes);
                ValidateBoxes(move.MoveId, frame.Frame, "hurtbox", frame.Hurtboxes);
            }

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

    private static void ValidateBoxes(
        string moveId, int frame, string kind, IReadOnlyList<CollisionBoxDefinition> boxes)
    {
        var ids = new HashSet<string>();
        foreach (var box in boxes)
        {
            if (box is null)
                throw new FormatException($"[Data] Move '{moveId}' frame {frame}: {kind} list cannot contain null entries.");
            if (string.IsNullOrWhiteSpace(box.BoxId) || !ids.Add(box.BoxId))
                throw new FormatException($"[Data] Move '{moveId}' frame {frame}: {kind} box_id must be non-empty and unique.");
            if (!float.IsFinite(box.X) || !float.IsFinite(box.Y) ||
                !float.IsFinite(box.Width) || !float.IsFinite(box.Height))
                throw new FormatException($"[Data] Move '{moveId}' frame {frame}: {kind} '{box.BoxId}' values must be finite.");
            if (box.Width < 0 || box.Height < 0)
                throw new FormatException($"[Data] Move '{moveId}' frame {frame}: {kind} '{box.BoxId}' dimensions must be >= 0.");
        }
    }

    private sealed class MoveListWrapper
    {
        [JsonPropertyName("moves")]
        public List<MoveDefinition> Moves { get; set; } = new();
    }
}
