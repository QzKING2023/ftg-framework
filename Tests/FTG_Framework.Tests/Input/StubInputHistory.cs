#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Tests;

internal sealed class StubInputHistory : IInputHistory
{
    private readonly Dictionary<int, List<InputEntry>> _directional = new();
    private readonly Dictionary<int, List<InputEntry>> _button = new();

    public void AddDirectionalEntry(int playerId, int frame, DirectionValue dir)
    {
        if (!_directional.ContainsKey(playerId))
            _directional[playerId] = new List<InputEntry>();
        _directional[playerId].Add(new InputEntry(frame, InputType.Directional, (int)dir));
    }

    public IReadOnlyList<InputEntry> GetDirectionalHistory(int playerId)
    {
        if (_directional.TryGetValue(playerId, out var list))
            return list.AsReadOnly();
        return new List<InputEntry>().AsReadOnly();
    }

    public IReadOnlyList<InputEntry> GetButtonHistory(int playerId)
    {
        if (_button.TryGetValue(playerId, out var list))
            return list.AsReadOnly();
        return new List<InputEntry>().AsReadOnly();
    }

    public IReadOnlyList<InputEntry> GetHistory(int playerId, InputType type)
    {
        return type switch
        {
            InputType.Directional => GetDirectionalHistory(playerId),
            InputType.Button => GetButtonHistory(playerId),
            _ => new List<InputEntry>().AsReadOnly()
        };
    }

    public int Capacity => 600;

    public void RecordInput(int playerId, InputType type, int value)
    {
    }

    public IDataStore DataStore => null!;
    public void Initialize(IDataStore dataStore) { }
    public void Shutdown() { }
}
