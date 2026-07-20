#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.Tests;

internal sealed class StubDataStore : IDataStore
{
    private readonly Dictionary<string, MoveDefinition> _moves = new();

    public void SetMove(MoveDefinition move) => _moves[move.MoveId] = move;

    public MoveDefinition? GetMove(string moveId) =>
        _moves.TryGetValue(moveId, out var move) ? move : null;

    public IReadOnlyList<MoveDefinition> GetAllMoves() => new List<MoveDefinition>(_moves.Values);
}
