#nullable enable
using System;
using System.Text.Json;

namespace FTG_Framework.Core;

/// <summary>
/// Core-owned adapter for runtime owners whose prepared state can be installed by
/// a validation-complete, allocation-free reference/field swap.
/// </summary>
internal sealed class RuntimeStateSnapshotParticipant<TState> : IStateSnapshotParticipant
    where TState : class
{
    private readonly Func<int, ulong, TState> _capture;
    private readonly Func<TState, SnapshotPrepareContext, TState> _prepare;
    private readonly Action<TState> _install;
    private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = false };

    internal RuntimeStateSnapshotParticipant(string discriminator, int codecVersion,
        Func<int, ulong, TState> capture, Func<TState, SnapshotPrepareContext, TState> prepare,
        Action<TState> install)
    {
        Discriminator = discriminator;
        CodecVersion = codecVersion;
        _capture = capture;
        _prepare = prepare;
        _install = install;
    }

    public string Discriminator { get; }
    public int CodecVersion { get; }

    public SnapshotComponent Capture(int frame, ulong epoch) =>
        new(Discriminator, CodecVersion, JsonSerializer.Serialize(_capture(frame, epoch), _options));

    public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
    {
        TState decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<TState>(component.Payload, _options)
                ?? throw new SnapshotPrepareException(Discriminator, "Codec returned null.");
        }
        catch (JsonException ex)
        {
            throw new SnapshotPrepareException(Discriminator, "Invalid component payload.", ex);
        }
        TState prepared = _prepare(decoded, context)
            ?? throw new SnapshotPrepareException(Discriminator, "Prepare returned null.");
        return PreparedSnapshotComponent.CreateOwnerSwap(Discriminator, prepared, _install);
    }
}
