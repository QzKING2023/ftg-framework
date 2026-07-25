#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Engine.Combo;

internal sealed class ChainValidator
{
    private readonly IDataStore _dataStore;
    private readonly Dictionary<int, List<string>> _playerChains = new();

    public ChainValidator(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public bool IsUnique(int playerId, string moveId)
    {
        ArgumentNullException.ThrowIfNull(moveId);

        var moveDef = _dataStore.GetMove(moveId);
        if (moveDef is null)
            return true;

        if (moveDef.ChainRepeatable)
            return true;

        if (_playerChains.TryGetValue(playerId, out var chain) && chain.Contains(moveId))
            return false;

        return true;
    }

    public void AddToChain(int playerId, string moveId)
    {
        ArgumentNullException.ThrowIfNull(moveId);

        var moveDef = _dataStore.GetMove(moveId);
        if (moveDef is { ChainRepeatable: true })
            return;

        if (!_playerChains.TryGetValue(playerId, out var chain))
        {
            chain = new List<string>();
            _playerChains[playerId] = chain;
        }

        chain.Add(moveId);
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
