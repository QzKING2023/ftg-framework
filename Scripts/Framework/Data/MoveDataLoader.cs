#nullable enable
using System;
using System.Linq;
using Godot;
using FileAccess = Godot.FileAccess;

namespace FTG_Framework.Data;

internal static class MoveDataLoader
{
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
        return MoveDatasetCodec.Parse(json).Moves.ToArray();
    }
}
