#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FTG_Framework.Data;

internal static class MoveDatasetCodec
{
    internal const int CurrentSchemaVersion = 1;

    internal static MoveDatasetDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        byte[] source = Encoding.UTF8.GetBytes(json);
        try
        {
            using var parsed = JsonDocument.Parse(source, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow });
            RejectDuplicateProperties(parsed.RootElement, "$", new HashSet<string>(StringComparer.Ordinal));
            RequireKind(parsed.RootElement, JsonValueKind.Object, "$", "Use a JSON object.");
            int version = ReadRequiredInt(parsed.RootElement, "schema_version", "schema_version");
            if (version != CurrentSchemaVersion)
                Fail("schema_version", version.ToString(), $"unsupported schema version {version}", $"Use schema_version {CurrentSchemaVersion}.");
            var movesElement = Required(parsed.RootElement, "moves", "moves");
            RequireKind(movesElement, JsonValueKind.Array, "moves", "Provide a non-null moves array.");
            var moves = new List<MoveDefinition>();
            int index = 0;
            foreach (var element in movesElement.EnumerateArray())
                moves.Add(ReadMove(element, index++));
            ValidateRuntimeMoves(moves);
            return new MoveDatasetDocument(version, new ReadOnlyCollection<MoveDefinition>(moves),
                MoveContentIdentity.FromBytes(source));
        }
        catch (MoveDatasetFormatException) { throw; }
        catch (JsonException ex)
        {
            throw new MoveDatasetFormatException("$", ex.Message, "invalid JSON", "Correct the JSON syntax.", ex);
        }
    }

    internal static byte[] Serialize(MoveDatasetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != CurrentSchemaVersion)
            Fail("schema_version", document.SchemaVersion.ToString(), "unsupported schema version", $"Use schema_version {CurrentSchemaVersion}.");
        return SerializeMovesForComparison(document.Moves);
    }

    internal static byte[] SerializeMovesForComparison(IEnumerable<MoveDefinition> moves)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", CurrentSchemaVersion);
            writer.WritePropertyName("moves");
            writer.WriteStartArray();
            foreach (var move in moves)
                WriteMove(writer, move);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static MoveValidationResult Validate(MoveAuthoringCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var errors = new List<MoveValidationError>();
        if (candidate.SchemaVersion != CurrentSchemaVersion)
            Add(errors, "schema_version", candidate.SchemaVersion.ToString(), "unsupported schema version",
                $"Use schema_version {CurrentSchemaVersion}.");
        if (candidate.Moves is null)
        {
            Add(errors, "moves", "null", "moves collection is required", "Provide a non-null moves collection.");
            return new MoveValidationResult(new ReadOnlyCollection<MoveValidationError>(errors));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < candidate.Moves.Count; i++)
        {
            MoveAuthoringMove? move = candidate.Moves[i];
            string root = $"moves[{i}]";
            if (move is null)
            {
                Add(errors, root, "null", "null move entry", "Remove or replace the null entry.");
                continue;
            }
            ValidateCandidateMove(move, root, ids, errors);
        }
        return errors.Count == 0
            ? MoveValidationResult.Valid
            : new MoveValidationResult(new ReadOnlyCollection<MoveValidationError>(errors));
    }

    private static void ValidateCandidateMove(
        MoveAuthoringMove move, string root, HashSet<string> ids, List<MoveValidationError> errors)
    {
        if (string.IsNullOrEmpty(move.MoveId))
            Add(errors, $"{root}.move_id", move.MoveId ?? "null", "identifier must be non-empty", "Enter a non-empty ordinal move_id.");
        else if (!ids.Add(move.MoveId))
            Add(errors, $"{root}.move_id", move.MoveId, "duplicate move identifier", "Use a unique ordinal move_id.");
        if (move.Startup < 0) Add(errors, $"{root}.startup", move.Startup.ToString(), "must be non-negative", "Enter a non-negative frame count.");
        if (move.Active < 0) Add(errors, $"{root}.active", move.Active.ToString(), "must be non-negative", "Enter a non-negative frame count.");
        if (move.Recovery < 0) Add(errors, $"{root}.recovery", move.Recovery.ToString(), "must be non-negative", "Enter a non-negative frame count.");
        if (move.Damage < 0) Add(errors, $"{root}.damage", move.Damage.ToString(), "must be non-negative", "Enter non-negative damage.");
        if (string.IsNullOrEmpty(move.KnockbackProfileId))
            Add(errors, $"{root}.knockback_profile_id", move.KnockbackProfileId ?? "null", "identifier must be non-empty", "Select a non-empty ordinal knockback_profile_id.");

        int total = 0;
        bool validTotal = true;
        try { total = checked(move.Startup + move.Active + move.Recovery); }
        catch (OverflowException)
        {
            validTotal = false;
            Add(errors, root, "duration overflow", "total frames exceed Int32", "Reduce timing fields.");
        }

        if (move.CancelWindows is null)
            Add(errors, $"{root}.cancel_windows", "null", "collection is required", "Provide a non-null cancel_windows collection.");
        else
            for (int i = 0; i < move.CancelWindows.Count; i++)
            {
                CancelWindow? window = move.CancelWindows[i];
                string path = $"{root}.cancel_windows[{i}]";
                if (window is null)
                {
                    Add(errors, path, "null", "null entry", "Remove or replace the null entry.");
                    continue;
                }
                if (string.IsNullOrEmpty(window.TargetCategory))
                    Add(errors, $"{path}.target_category", window.TargetCategory ?? "null", "identifier must be non-empty", "Enter a non-empty ordinal target_category.");
                if (window.StartFrame < 0 || window.EndFrame < window.StartFrame || (validTotal && window.EndFrame > total))
                    Add(errors, path, $"{window.StartFrame}..{window.EndFrame}", "invalid range",
                        validTotal ? $"Use 0 <= start_frame <= end_frame <= {total}." : "Correct move timing before validating this range.");
            }

        if (move.CollisionFrames is null)
        {
            Add(errors, $"{root}.collision_frames", "null", "collection is required", "Provide a non-null collision_frames collection.");
            return;
        }
        var frames = new HashSet<int>();
        for (int i = 0; i < move.CollisionFrames.Count; i++)
        {
            CollisionFrameDefinition? frame = move.CollisionFrames[i];
            string path = $"{root}.collision_frames[{i}]";
            if (frame is null)
            {
                Add(errors, path, "null", "null entry", "Remove or replace the null entry.");
                continue;
            }
            if (frame.Frame < 1 || (validTotal && frame.Frame > total))
                Add(errors, $"{path}.frame", frame.Frame.ToString(), "outside move duration",
                    validTotal ? $"Use a frame in 1..{total}." : "Correct move timing before validating this frame.");
            if (!frames.Add(frame.Frame))
                Add(errors, $"{path}.frame", frame.Frame.ToString(), "duplicate frame", "Use each collision frame once.");
            ValidateCandidateBoxes(frame.Hitboxes, $"{path}.hitboxes", errors);
            ValidateCandidateBoxes(frame.Hurtboxes, $"{path}.hurtboxes", errors);
        }
    }

    private static void ValidateCandidateBoxes(
        IReadOnlyList<CollisionBoxDefinition>? boxes, string path, List<MoveValidationError> errors)
    {
        if (boxes is null)
        {
            Add(errors, path, "null", "collection is required", "Provide a non-null box collection.");
            return;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < boxes.Count; i++)
        {
            CollisionBoxDefinition? box = boxes[i];
            string itemPath = $"{path}[{i}]";
            if (box is null)
            {
                Add(errors, itemPath, "null", "null entry", "Remove or replace the null entry.");
                continue;
            }
            if (string.IsNullOrEmpty(box.BoxId))
                Add(errors, $"{itemPath}.box_id", box.BoxId ?? "null", "identifier must be non-empty", "Enter a non-empty ordinal box_id.");
            else if (!ids.Add(box.BoxId))
                Add(errors, $"{itemPath}.box_id", box.BoxId, "duplicate box identifier", "Use a unique box_id in this list.");
            if (!float.IsFinite(box.X)) Add(errors, $"{itemPath}.x", box.X.ToString(), "must be finite", "Enter a finite x coordinate.");
            if (!float.IsFinite(box.Y)) Add(errors, $"{itemPath}.y", box.Y.ToString(), "must be finite", "Enter a finite y coordinate.");
            if (!float.IsFinite(box.Width)) Add(errors, $"{itemPath}.width", box.Width.ToString(), "must be finite", "Enter a finite width.");
            else if (box.Width < 0) Add(errors, $"{itemPath}.width", box.Width.ToString(), "must be non-negative", "Enter a non-negative width.");
            if (!float.IsFinite(box.Height)) Add(errors, $"{itemPath}.height", box.Height.ToString(), "must be finite", "Enter a finite height.");
            else if (box.Height < 0) Add(errors, $"{itemPath}.height", box.Height.ToString(), "must be non-negative", "Enter a non-negative height.");
        }
    }

    private static void Add(List<MoveValidationError> errors, string path, string rejected, string message, string recovery) =>
        errors.Add(new MoveValidationError(path, rejected, message, recovery));

    private static MoveDefinition ReadMove(JsonElement element, int index)
    {
        string root = $"moves[{index}]";
        RequireKind(element, JsonValueKind.Object, root, "Provide a move object.");
        string moveId = ReadRequiredString(element, "move_id", $"{root}.move_id");
        int startup = ReadRequiredInt(element, "startup", $"{root}.startup");
        int active = ReadRequiredInt(element, "active", $"{root}.active");
        int recovery = ReadRequiredInt(element, "recovery", $"{root}.recovery");
        int hitAdvantage = ReadRequiredInt(element, "hit_advantage", $"{root}.hit_advantage");
        int blockAdvantage = ReadRequiredInt(element, "block_advantage", $"{root}.block_advantage");
        int damage = ReadRequiredInt(element, "damage", $"{root}.damage");
        bool chainRepeatable = ReadRequiredBool(element, "chain_repeatable", $"{root}.chain_repeatable");
        string knockback = ReadRequiredString(element, "knockback_profile_id", $"{root}.knockback_profile_id");
        var cancelWindows = ReadArray(element, "cancel_windows", $"{root}.cancel_windows", (item, i) => ReadCancel(item, $"{root}.cancel_windows[{i}]"));
        var collisionFrames = ReadArray(element, "collision_frames", $"{root}.collision_frames", (item, i) => ReadCollisionFrame(item, $"{root}.collision_frames[{i}]"));
        string? moveName = null;
        if (element.TryGetProperty("move_name", out var name))
        {
            RequireKind(name, JsonValueKind.String, $"{root}.move_name", "Use a string or omit move_name.");
            moveName = name.GetString();
        }
        return new MoveDefinition
        {
            MoveId = moveId,
            Startup = startup,
            Active = active,
            Recovery = recovery,
            HitAdvantage = hitAdvantage,
            BlockAdvantage = blockAdvantage,
            Damage = damage,
            ChainRepeatable = chainRepeatable,
            KnockbackProfileId = knockback,
            MoveName = moveName,
            CancelWindows = cancelWindows,
            CollisionFrames = collisionFrames,
        };
    }

    private static CancelWindow ReadCancel(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path, "Provide a cancel-window object.");
        return new CancelWindow
        {
            StartFrame = ReadRequiredInt(element, "start_frame", $"{path}.start_frame"),
            EndFrame = ReadRequiredInt(element, "end_frame", $"{path}.end_frame"),
            TargetCategory = ReadRequiredString(element, "target_category", $"{path}.target_category"),
        };
    }

    private static CollisionFrameDefinition ReadCollisionFrame(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path, "Provide a collision-frame object.");
        return new CollisionFrameDefinition
        {
            Frame = ReadRequiredInt(element, "frame", $"{path}.frame"),
            Hitboxes = ReadArray(element, "hitboxes", $"{path}.hitboxes", (item, i) => ReadBox(item, $"{path}.hitboxes[{i}]")),
            Hurtboxes = ReadArray(element, "hurtboxes", $"{path}.hurtboxes", (item, i) => ReadBox(item, $"{path}.hurtboxes[{i}]")),
        };
    }

    private static CollisionBoxDefinition ReadBox(JsonElement element, string path)
    {
        RequireKind(element, JsonValueKind.Object, path, "Provide a collision-box object.");
        return new CollisionBoxDefinition
        {
            BoxId = ReadRequiredString(element, "box_id", $"{path}.box_id"),
            X = ReadRequiredFloat(element, "x", $"{path}.x"),
            Y = ReadRequiredFloat(element, "y", $"{path}.y"),
            Width = ReadRequiredFloat(element, "width", $"{path}.width"),
            Height = ReadRequiredFloat(element, "height", $"{path}.height"),
        };
    }

    private static void ValidateRuntimeMoves(IReadOnlyList<MoveDefinition> moves)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            string root = $"moves[{i}]";
            if (!ids.Add(move.MoveId)) Fail($"{root}.move_id", move.MoveId, "duplicate move identifier", "Use a unique ordinal move_id.");
            if (move.Startup < 0) Fail($"{root}.startup", move.Startup.ToString(), "must be non-negative", "Enter a non-negative frame count.");
            if (move.Active < 0) Fail($"{root}.active", move.Active.ToString(), "must be non-negative", "Enter a non-negative frame count.");
            if (move.Recovery < 0) Fail($"{root}.recovery", move.Recovery.ToString(), "must be non-negative", "Enter a non-negative frame count.");
            if (move.Damage < 0) Fail($"{root}.damage", move.Damage.ToString(), "must be non-negative", "Enter non-negative damage.");
            int total;
            try { total = checked(move.Startup + move.Active + move.Recovery); }
            catch (OverflowException) { Fail(root, "duration overflow", "total frames exceed Int32", "Reduce timing fields."); return; }
            var collisionIds = new HashSet<int>();
            foreach (var frame in move.CollisionFrames)
            {
                if (frame is null) Fail($"{root}.collision_frames", "null", "null entry", "Remove or replace the null entry.");
                if (frame.Frame < 1 || frame.Frame > total) Fail($"{root}.collision_frames.frame", frame.Frame.ToString(), "outside move duration", $"Use a frame in 1..{total}.");
                if (!collisionIds.Add(frame.Frame)) Fail($"{root}.collision_frames.frame", frame.Frame.ToString(), "duplicate frame", "Use each collision frame once.");
                ValidateBoxes(frame.Hitboxes, $"{root}.collision_frames.hitboxes");
                ValidateBoxes(frame.Hurtboxes, $"{root}.collision_frames.hurtboxes");
            }
            foreach (var window in move.CancelWindows)
            {
                if (window is null) Fail($"{root}.cancel_windows", "null", "null entry", "Remove or replace the null entry.");
                if (string.IsNullOrEmpty(window.TargetCategory))
                    Fail($"{root}.cancel_windows.target_category", window.TargetCategory ?? "null", "identifier must be non-empty", "Enter a non-empty ordinal target_category.");
                if (window.StartFrame < 0 || window.EndFrame < window.StartFrame || window.EndFrame > total)
                    Fail($"{root}.cancel_windows", $"{window.StartFrame}..{window.EndFrame}", "invalid range", $"Use 0 <= start_frame <= end_frame <= {total}.");
            }
        }
    }

    private static void ValidateBoxes(IReadOnlyList<CollisionBoxDefinition> boxes, string path)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var box in boxes)
        {
            if (box is null) Fail(path, "null", "null entry", "Remove or replace the null entry.");
            if (string.IsNullOrEmpty(box.BoxId)) Fail($"{path}.box_id", box.BoxId ?? "null", "identifier must be non-empty", "Enter a non-empty ordinal box_id.");
            if (!ids.Add(box.BoxId)) Fail($"{path}.box_id", box.BoxId, "duplicate box identifier", "Use a unique box_id in this list.");
            if (!float.IsFinite(box.X) || !float.IsFinite(box.Y) || !float.IsFinite(box.Width) || !float.IsFinite(box.Height))
                Fail(path, "non-finite", "coordinates and dimensions must be finite", "Enter finite numbers.");
            if (box.Width < 0 || box.Height < 0) Fail(path, "negative dimension", "dimensions must be non-negative", "Enter non-negative dimensions.");
        }
    }

    private static void WriteMove(Utf8JsonWriter writer, MoveDefinition move)
    {
        writer.WriteStartObject();
        writer.WriteString("move_id", move.MoveId);
        if (move.MoveName is not null) writer.WriteString("move_name", move.MoveName);
        writer.WriteNumber("startup", move.Startup); writer.WriteNumber("active", move.Active); writer.WriteNumber("recovery", move.Recovery);
        writer.WriteNumber("hit_advantage", move.HitAdvantage); writer.WriteNumber("block_advantage", move.BlockAdvantage); writer.WriteNumber("damage", move.Damage);
        writer.WriteBoolean("chain_repeatable", move.ChainRepeatable); writer.WriteString("knockback_profile_id", move.KnockbackProfileId);
        writer.WritePropertyName("cancel_windows"); writer.WriteStartArray();
        foreach (var window in move.CancelWindows) { writer.WriteStartObject(); writer.WriteNumber("start_frame", window.StartFrame); writer.WriteNumber("end_frame", window.EndFrame); writer.WriteString("target_category", window.TargetCategory); writer.WriteEndObject(); }
        writer.WriteEndArray();
        writer.WritePropertyName("collision_frames"); writer.WriteStartArray();
        foreach (var frame in move.CollisionFrames) { writer.WriteStartObject(); writer.WriteNumber("frame", frame.Frame); WriteBoxes(writer, "hitboxes", frame.Hitboxes); WriteBoxes(writer, "hurtboxes", frame.Hurtboxes); writer.WriteEndObject(); }
        writer.WriteEndArray(); writer.WriteEndObject();
    }

    private static void WriteBoxes(Utf8JsonWriter writer, string name, IReadOnlyList<CollisionBoxDefinition> boxes)
    {
        writer.WritePropertyName(name); writer.WriteStartArray();
        foreach (var box in boxes) { writer.WriteStartObject(); writer.WriteString("box_id", box.BoxId); writer.WriteNumber("x", box.X); writer.WriteNumber("y", box.Y); writer.WriteNumber("width", box.Width); writer.WriteNumber("height", box.Height); writer.WriteEndObject(); }
        writer.WriteEndArray();
    }

    private static IReadOnlyList<T> ReadArray<T>(JsonElement parent, string name, string path, Func<JsonElement, int, T> read)
    {
        var element = Required(parent, name, path); RequireKind(element, JsonValueKind.Array, path, "Provide a non-null array.");
        var values = new List<T>(); int index = 0; foreach (var item in element.EnumerateArray()) values.Add(read(item, index++));
        return new ReadOnlyCollection<T>(values);
    }

    private static JsonElement Required(JsonElement parent, string name, string path)
    { if (!parent.TryGetProperty(name, out var value)) Fail(path, "missing", "required field is absent", $"Add the required {name} field."); return value; }
    private static int ReadRequiredInt(JsonElement parent, string name, string path)
    { var value = Required(parent, name, path); int result = default; if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out result)) Fail(path, value.GetRawText(), "expected an Int32 JSON number", "Enter an integral number in the Int32 range."); return result; }
    private static float ReadRequiredFloat(JsonElement parent, string name, string path)
    { var value = Required(parent, name, path); float result = default; if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out result) || !float.IsFinite(result)) Fail(path, value.GetRawText(), "expected a finite JSON number", "Enter a finite number."); return result; }
    private static bool ReadRequiredBool(JsonElement parent, string name, string path)
    { var value = Required(parent, name, path); if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) Fail(path, value.GetRawText(), "expected a JSON boolean", "Enter true or false."); return value.GetBoolean(); }
    private static string ReadRequiredString(JsonElement parent, string name, string path)
    { var value = Required(parent, name, path); RequireKind(value, JsonValueKind.String, path, "Enter a non-empty string."); string result = value.GetString()!; if (string.IsNullOrEmpty(result)) Fail(path, result, "identifier must be non-empty", "Enter a non-empty ordinal identifier."); return result; }
    private static void RequireKind(JsonElement element, JsonValueKind kind, string path, string recovery)
    { if (element.ValueKind != kind) Fail(path, element.ValueKind.ToString(), $"expected {kind}", recovery); }
    private static void RejectDuplicateProperties(JsonElement element, string path, HashSet<string> scratch)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            scratch = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!scratch.Add(property.Name)) Fail($"{path}.{property.Name}.duplicate", property.Name, "duplicate property", "Remove the duplicate property."); RejectDuplicateProperties(property.Value, $"{path}.{property.Name}", scratch); }
        }
        else if (element.ValueKind == JsonValueKind.Array) { int i = 0; foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child, $"{path}[{i++}]", scratch); }
    }
    [DoesNotReturn]
    private static void Fail(string path, string rejected, string message, string recovery) => throw new MoveDatasetFormatException(path, rejected, message, recovery);
}
