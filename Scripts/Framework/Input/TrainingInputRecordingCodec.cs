#nullable enable
using System;
using System.IO;
using System.Text.Json;
using FTG_Framework.Core;

namespace FTG_Framework.Input;

public static class TrainingInputRecordingCodec
{
    public static byte[] Encode(TrainingInputRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", recording.SchemaVersion);
            writer.WriteNumber("source_player", recording.SourcePlayer);
            writer.WriteNumber("duration_frames", recording.DurationFrames);
            writer.WriteString("name", recording.Name);
            writer.WriteStartArray("entries");
            foreach (var entry in recording.Entries)
            {
                writer.WriteStartObject();
                writer.WriteNumber("relative_frame", entry.RelativeFrame);
                writer.WriteNumber("within_frame_sequence", entry.WithinFrameSequence);
                writer.WriteNumber("input_type", (int)entry.InputType);
                writer.WriteNumber("input_value", entry.InputValue);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        if (stream.Length > TrainingInputRecording.MaxEncodedBytes)
            throw new TrainingInputRecordingException(
                $"[Input] Encoded payload exceeds {TrainingInputRecording.MaxEncodedBytes} bytes.");
        return stream.ToArray();
    }

    public static TrainingInputRecording Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is 0 or > TrainingInputRecording.MaxEncodedBytes)
            throw new TrainingInputRecordingException(
                $"[Input] Encoded payload length {payload.Length} is invalid.");
        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Error("Root must be an object.");
            int schema = RequiredInt(root, "schema_version");
            int source = RequiredInt(root, "source_player");
            int duration = RequiredInt(root, "duration_frames");
            if (!root.TryGetProperty("name", out var nameNode) ||
                nameNode.ValueKind != JsonValueKind.String)
                throw Error("name must be a string.");
            string name = nameNode.GetString() ?? string.Empty;
            if (!root.TryGetProperty("entries", out var entriesNode) || entriesNode.ValueKind != JsonValueKind.Array)
                throw Error("Entries must be an array.");
            if (entriesNode.GetArrayLength() > TrainingInputRecording.MaxEntries)
                throw Error($"Entry count exceeds {TrainingInputRecording.MaxEntries}.");
            var entries = new TrainingInputRecordingEntry[entriesNode.GetArrayLength()];
            int index = 0;
            foreach (JsonElement node in entriesNode.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object) throw Error("Entry must be an object.");
                entries[index++] = new TrainingInputRecordingEntry(
                    RequiredInt(node, "relative_frame"),
                    RequiredInt(node, "within_frame_sequence"),
                    (InputType)RequiredInt(node, "input_type"),
                    RequiredInt(node, "input_value"));
            }
            return new TrainingInputRecording(schema, source, duration, name, entries);
        }
        catch (TrainingInputRecordingException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OverflowException)
        {
            throw new TrainingInputRecordingException("[Input] Recording payload is malformed.", ex);
        }
    }

    private static int RequiredInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement node) ||
            node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out int value))
            throw Error($"{name} must be an Int32.");
        return value;
    }

    private static TrainingInputRecordingException Error(string message) => new($"[Input] {message}");
}
