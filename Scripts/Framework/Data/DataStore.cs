#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;

namespace FTG_Framework.Data;

internal sealed class DataStore : IDataStore
{
    private readonly Dictionary<string, MoveDefinition> _moves;
    private readonly Dictionary<string, GatlingTable> _gatlingTables;
    private readonly HashSet<string> _knownCategories;

    public DataStore(MoveDefinition[] moves, GatlingTable[]? gatlingTables = null)
    {
        ArgumentNullException.ThrowIfNull(moves);
        _moves = new Dictionary<string, MoveDefinition>();
        foreach (var move in moves)
            _moves[move.MoveId] = move;

        _knownCategories = new HashSet<string>();
        foreach (var move in moves)
        {
            foreach (var cw in move.CancelWindows)
            {
                if (!string.IsNullOrEmpty(cw.TargetCategory))
                    _knownCategories.Add(cw.TargetCategory);
            }
        }

        _gatlingTables = new Dictionary<string, GatlingTable>();
        if (gatlingTables is not null)
        {
            foreach (var table in gatlingTables)
                _gatlingTables[table.CharacterId] = ValidateTable(table);
        }
    }

    public void SetGatlingTable(GatlingTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrEmpty(table.CharacterId))
            throw new FormatException("[Data] Gatling table has null or empty character_id.");
        _gatlingTables[table.CharacterId] = ValidateTable(table);
    }

    private GatlingTable ValidateTable(GatlingTable table)
    {
        if (table.Entries is null)
            throw new FormatException($"[Data] Gatling table '{table.CharacterId}': entries is null.");

        var validatedEntries = new List<GatlingEntry>();
        foreach (var entry in table.Entries)
        {
            if (entry.TargetMoves is null)
                throw new FormatException($"[Data] Gatling table '{table.CharacterId}': entry for source_move '{entry.SourceMove}' has null target_moves.");

            if (!_moves.ContainsKey(entry.SourceMove))
            {
                FrameworkLog.Error?.Invoke($"[Data] Gatling table '{table.CharacterId}': source_move '{entry.SourceMove}' not found in registered moves. Entry ignored.");
                continue;
            }

            if (!_knownCategories.Contains(entry.CancelCategory))
            {
                FrameworkLog.Error?.Invoke($"[Data] Gatling table '{table.CharacterId}': cancel_category '{entry.CancelCategory}' in entry for '{entry.SourceMove}' not found in any registered move's cancel windows. Entry ignored.");
                continue;
            }

            if (entry.TargetMoves.Contains(entry.SourceMove))
            {
                if (!_moves.TryGetValue(entry.SourceMove, out var moveDef) || !moveDef.ChainRepeatable)
                    throw new FormatException($"[Data] Gatling table '{table.CharacterId}': self-cancel entry for source_move '{entry.SourceMove}' requires chain_repeatable=true on the move definition.");
            }

            var validTargets = new List<string>();
            foreach (var target in entry.TargetMoves)
            {
                if (string.IsNullOrEmpty(target))
                    continue;

                if (!_moves.ContainsKey(target))
                {
                    FrameworkLog.Error?.Invoke($"[Data] Gatling table '{table.CharacterId}': target_move '{target}' in entry for '{entry.SourceMove}' not found in registered moves. Target ignored.");
                    continue;
                }
                validTargets.Add(target);
            }

            if (validTargets.Count > 0)
            {
                validatedEntries.Add(new GatlingEntry
                {
                    SourceMove = entry.SourceMove,
                    TargetMoves = validTargets,
                    CancelCategory = entry.CancelCategory
                });
            }
        }

        return new GatlingTable
        {
            CharacterId = table.CharacterId,
            Entries = validatedEntries
        };
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

    public GatlingTable? GetGatlingTable(string characterId)
    {
        if (characterId is null)
            return null;
        if (_gatlingTables.TryGetValue(characterId, out var table))
            return table;
        return new GatlingTable { CharacterId = characterId };
    }

    public IReadOnlyList<GatlingTable> GetAllGatlingTables()
    {
        return _gatlingTables.Values.ToList();
    }
}
