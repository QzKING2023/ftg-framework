#nullable enable
using System;
using System.Text.Json;

namespace FTG_Framework.Core;

/// <summary>
/// Owner-side adapter for a versioned value DTO. Decode and validation finish in Prepare;
/// Commit invokes only the pre-bound reference installer with the final replacement.
/// </summary>
public sealed class JsonStateSnapshotParticipant<TState> : IStateSnapshotParticipant where TState : class
{
    private readonly SnapshotReference<TState> _slot;
    private readonly Func<TState, SnapshotPrepareContext, TState> _prepare;
    private readonly JsonSerializerOptions _options;

    public JsonStateSnapshotParticipant(string discriminator, int codecVersion,
        SnapshotReference<TState> slot, Func<TState, SnapshotPrepareContext, TState> prepare,
        JsonSerializerOptions? options = null)
    {
        Discriminator = string.IsNullOrWhiteSpace(discriminator)
            ? throw new ArgumentException("Discriminator is required.", nameof(discriminator))
            : discriminator;
        CodecVersion = codecVersion > 0 ? codecVersion : throw new ArgumentOutOfRangeException(nameof(codecVersion));
        _slot = slot ?? throw new ArgumentNullException(nameof(slot));
        _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        _options = options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
    }

    public string Discriminator { get; }
    public int CodecVersion { get; }

    public SnapshotComponent Capture(int frame, ulong epoch)
    {
        TState dto = _slot.Value;
        return new SnapshotComponent(Discriminator, CodecVersion, JsonSerializer.Serialize(dto, _options));
    }

    public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
    {
        TState decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<TState>(component.Payload, _options)
                ?? throw new SnapshotPrepareException(Discriminator, "Codec returned null.");
        }
        catch (JsonException ex) { throw new SnapshotPrepareException(Discriminator, "Invalid component payload.", ex); }
        TState replacement = _prepare(decoded, context)
            ?? throw new SnapshotPrepareException(Discriminator, "Prepare returned null.");
        return PreparedSnapshotComponent.Create(Discriminator, _slot, replacement);
    }
}

/// <summary>Owner-held state reference; prepared Commit performs only this reference assignment.</summary>
public sealed class SnapshotReference<TState> where TState : class
{
    public SnapshotReference(TState value) => Value = value ?? throw new ArgumentNullException(nameof(value));
    public TState Value { get; set; }
}
