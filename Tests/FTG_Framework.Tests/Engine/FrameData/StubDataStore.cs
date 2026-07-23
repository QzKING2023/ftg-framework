#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.Tests;

internal sealed class StubDataStore : IDataStore
{
    private readonly Dictionary<string, MoveDefinition> _moves = new();
    private readonly Dictionary<string, GatlingTable> _gatlingTables = new();

    public void SetMove(MoveDefinition move) => _moves[move.MoveId] = move;

    public void SetGatlingTable(GatlingTable table) => _gatlingTables[table.CharacterId] = table;

    public MoveDefinition? GetMove(string moveId) =>
        _moves.TryGetValue(moveId, out var move) ? move : null;

    public IReadOnlyList<MoveDefinition> GetAllMoves() => new List<MoveDefinition>(_moves.Values);

    public GatlingTable? GetGatlingTable(string characterId)
    {
        if (characterId is null)
            return null;
        if (_gatlingTables.TryGetValue(characterId, out var table))
            return table;
        return new GatlingTable { CharacterId = characterId };
    }

    public IReadOnlyList<GatlingTable> GetAllGatlingTables() =>
        new List<GatlingTable>(_gatlingTables.Values);
}
