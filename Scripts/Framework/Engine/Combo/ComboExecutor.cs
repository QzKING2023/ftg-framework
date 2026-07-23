#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.Combo;

internal sealed class ComboExecutor : IComboExecutor
{
    private readonly IDataStore _dataStore;
    private readonly Dictionary<string, GatlingTable> _windowSnapshots = new();

    public ComboExecutor(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public bool CanCancel(string characterId, string fromMoveId, string toMoveId, string cancelCategory)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        ArgumentNullException.ThrowIfNull(fromMoveId);
        ArgumentNullException.ThrowIfNull(toMoveId);
        ArgumentNullException.ThrowIfNull(cancelCategory);

        GatlingTable? table;
        if (_windowSnapshots.TryGetValue(characterId, out var snapshot))
            table = snapshot;
        else
            table = _dataStore.GetGatlingTable(characterId);

        if (table is null || table.Entries.Count == 0)
            return false;

        foreach (var entry in table.Entries)
        {
            if (entry.SourceMove == fromMoveId && entry.CancelCategory == cancelCategory)
                return entry.TargetMoves.Contains(toMoveId);
        }

        return false;
    }

    public void CaptureTableForWindow(string characterId)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        var table = _dataStore.GetGatlingTable(characterId);
        if (table is not null)
            _windowSnapshots[characterId] = table;
    }

    public void ReleaseTableForWindow(string characterId)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        _windowSnapshots.Remove(characterId);
    }
}
