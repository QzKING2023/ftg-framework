#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FTG_Framework.Core;

/// <summary>Canonical, bottom-to-top, transitively immutable state-stack value.</summary>
[JsonConverter(typeof(StateStackSnapshotJsonConverter))]
public sealed class StateStackSnapshot : IReadOnlyList<CharacterState>, IEquatable<StateStackSnapshot>
{
    private readonly CharacterState[] _states;

    public static StateStackSnapshot Empty { get; } = new([]);

    [JsonConstructor]
    public StateStackSnapshot(IReadOnlyList<CharacterState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        _states = states.ToArray();
    }

    public IReadOnlyList<CharacterState> States => Array.AsReadOnly(_states);
    public int Count => _states.Length;
    public CharacterState this[int index] => _states[index];
    public CharacterState TopOrIdle => _states.Length == 0 ? CharacterState.Idle : _states[^1];

    public IEnumerator<CharacterState> GetEnumerator() => ((IEnumerable<CharacterState>)_states).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(StateStackSnapshot? other) =>
        other is not null && _states.AsSpan().SequenceEqual(other._states);

    public override bool Equals(object? obj) => obj is StateStackSnapshot other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var state in _states)
            hash.Add(state);
        return hash.ToHashCode();
    }
}

public sealed class StateStackSnapshotJsonConverter : JsonConverter<StateStackSnapshot>
{
    public override StateStackSnapshot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        CharacterState[] states = JsonSerializer.Deserialize<CharacterState[]>(ref reader, options)
            ?? throw new JsonException("State snapshot cannot be null.");
        return new StateStackSnapshot(states);
    }

    public override void Write(Utf8JsonWriter writer, StateStackSnapshot value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.States, options);
}
