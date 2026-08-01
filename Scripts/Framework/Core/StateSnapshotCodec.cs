#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FTG_Framework.Core;

/// <summary>Canonical value-only UTF-8 codec for AD-20 state containers.</summary>
public static class StateSnapshotCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static byte[] Encode(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var dto = new ContainerDto(snapshot.SchemaVersion, snapshot.FrameworkVersion,
            snapshot.SourceEpoch, snapshot.Frame, snapshot.Components
                .OrderBy(c => c.Discriminator, StringComparer.Ordinal)
                .Select(c => new ComponentDto(c.Discriminator, c.CodecVersion, c.Payload)).ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }

    public static StateSnapshot Decode(ReadOnlySpan<byte> utf8)
    {
        ContainerDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ContainerDto>(utf8, Options)
                ?? throw new InvalidDataException("[Snapshot] Container is empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("[Snapshot] Invalid UTF-8 JSON container.", ex); }
        if (dto.Components is null)
            throw new InvalidDataException("[Snapshot] Components are required.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var components = new SnapshotComponent[dto.Components.Length];
        for (int i = 0; i < dto.Components.Length; i++)
        {
            ComponentDto? component = dto.Components[i];
            if (component is null)
                throw new InvalidDataException($"[Snapshot] Component at index {i} is null.");
            if (string.IsNullOrWhiteSpace(component.Discriminator) || !ids.Add(component.Discriminator))
                throw new InvalidDataException("[Snapshot] Component discriminators must be non-empty and unique.");
            if (component.CodecVersion < 1 || component.Payload is null)
                throw new InvalidDataException($"[Snapshot] Invalid component '{component.Discriminator}'.");
            try { using JsonDocument _ = JsonDocument.Parse(component.Payload); }
            catch (JsonException ex) { throw new InvalidDataException($"[Snapshot] Invalid payload for '{component.Discriminator}'.", ex); }
            components[i] = new SnapshotComponent(component.Discriminator, component.CodecVersion, component.Payload);
        }
        Array.Sort(components, (a, b) => string.CompareOrdinal(a.Discriminator, b.Discriminator));
        return new StateSnapshot(dto.SchemaVersion, dto.FrameworkVersion, dto.SourceEpoch, dto.Frame, components);
    }

    private sealed record ContainerDto(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("framework_version")] string FrameworkVersion,
        [property: JsonPropertyName("source_epoch")] ulong SourceEpoch,
        [property: JsonPropertyName("frame")] int Frame,
        [property: JsonPropertyName("components")] ComponentDto[] Components);
    private sealed record ComponentDto(
        [property: JsonPropertyName("discriminator")] string Discriminator,
        [property: JsonPropertyName("codec_version")] int CodecVersion,
        [property: JsonPropertyName("payload")] string Payload);
}
