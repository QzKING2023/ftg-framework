#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;

namespace FTG_Framework.Data;

internal sealed class DataStore : IDataStore
{
    private readonly Dictionary<string, MoveDefinition> _moves;

    public DataStore(MoveDefinition[] moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        _moves = new Dictionary<string, MoveDefinition>();
        foreach (var move in moves)
            _moves[move.MoveId] = move;
    }

    public MoveDefinition? GetMove(string moveId)
    {
        if (moveId is null)
            return null;
        _moves.TryGetValue(moveId, out var move);
        return move;
    }

    public IReadOnlyList<MoveDefinition> GetAllMoves()
    {
        return _moves.Values.ToList();
    }
}