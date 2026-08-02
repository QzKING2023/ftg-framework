#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Engine.Combo;

internal sealed class ChainValidator
{
    private readonly IDataStore _dataStore;
    private readonly Dictionary<int, List<ChainEntry>> _playerChains = new();
    private readonly record struct ChainEntry(string MoveId, bool Repeatable);

    public ChainValidator(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public bool IsUnique(int playerId, string moveId)
    {
        ArgumentNullException.ThrowIfNull(moveId);

        if (_playerChains.TryGetValue(playerId, out var chain))
        {
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if (string.Equals(chain[i].MoveId, moveId, StringComparison.Ordinal))
                    return chain[i].Repeatable;
            }
        }

        return true;
    }

    public void AddToChain(int playerId, string moveId)
    {
        ArgumentNullException.ThrowIfNull(moveId);

        if (!_playerChains.TryGetValue(playerId, out var chain))
        {
            chain = new List<ChainEntry>();
            _playerChains[playerId] = chain;
        }

        chain.Add(new ChainEntry(moveId, _dataStore.GetMove(moveId)?.ChainRepeatable ?? false));
    }

    public void AddToChainIfEmpty(int playerId, string moveId)
    {
        if (!_playerChains.TryGetValue(playerId, out var chain) || chain.Count == 0)
            AddToChain(playerId, moveId);
    }

    public void Clear()
    {
        _playerChains.Clear();
    }

    public void ResetForPlayer(int playerId)
    {
        _playerChains.Remove(playerId);
    }
}
