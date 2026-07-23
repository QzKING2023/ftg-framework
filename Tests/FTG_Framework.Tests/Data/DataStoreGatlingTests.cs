#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public class DataStoreGatlingTests
{
    private static MoveDefinition MakeMove(string moveId, bool chainRepeatable = false, string category = "normal") => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
        ChainRepeatable = chainRepeatable,
        CancelWindows = new List<CancelWindow>
        {
            new() { StartFrame = 3, EndFrame = 6, TargetCategory = category }
        }
    };

    private static GatlingTable MakeTable(string characterId, params GatlingEntry[] entries) => new()
    {
        CharacterId = characterId,
        Entries = new List<GatlingEntry>(entries)
    };

    private static GatlingEntry MakeEntry(string source, string[] targets, string category = "normal") => new()
    {
        SourceMove = source,
        TargetMoves = new List<string>(targets),
        CancelCategory = category
    };

    [Fact]
    public void GetGatlingTable_KnownCharacter_ReturnsTable()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP"), MakeMove("5HP") };
        var tables = new[] { MakeTable("ryu", MakeEntry("5LP", new[] { "5MP", "5HP" })) };
        var store = new DataStore(moves, tables);

        var table = store.GetGatlingTable("ryu");

        Assert.NotNull(table);
        Assert.Equal("ryu", table!.CharacterId);
        var entry = Assert.Single(table.Entries);
        Assert.Equal("5LP", entry.SourceMove);
        Assert.Equal(new[] { "5MP", "5HP" }, entry.TargetMoves);
    }

    [Fact]
    public void GetGatlingTable_UnknownCharacter_ReturnsEmptyTable()
    {
        var store = new DataStore(Array.Empty<MoveDefinition>());

        var table = store.GetGatlingTable("unregistered");

        Assert.NotNull(table);
        Assert.Equal("unregistered", table!.CharacterId);
        Assert.Empty(table.Entries);
    }

    [Fact]
    public void GetGatlingTable_NullCharacterId_ReturnsNull()
    {
        var store = new DataStore(Array.Empty<MoveDefinition>());

        var table = store.GetGatlingTable(null!);

        Assert.Null(table);
    }

    [Fact]
    public void GetAllGatlingTables_ReturnsAllRegistered()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP") };
        var tables = new[]
        {
            MakeTable("ryu", MakeEntry("5LP", new[] { "5MP" })),
            MakeTable("ken", MakeEntry("5LP", new[] { "5MP" }))
        };
        var store = new DataStore(moves, tables);

        var all = store.GetAllGatlingTables();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void SelfCancel_WithoutChainRepeatable_ThrowsFormatException()
    {
        var moves = new[] { MakeMove("5LP", chainRepeatable: false) };
        var tables = new[] { MakeTable("ryu", MakeEntry("5LP", new[] { "5LP" })) };

        var ex = Assert.Throws<FormatException>(() => new DataStore(moves, tables));
        Assert.Contains("[Data]", ex.Message);
        Assert.Contains("5LP", ex.Message);
        Assert.Contains("chain_repeatable", ex.Message);
    }

    [Fact]
    public void SelfCancel_WithChainRepeatable_Accepted()
    {
        var moves = new[] { MakeMove("5LP", chainRepeatable: true) };
        var tables = new[] { MakeTable("ryu", MakeEntry("5LP", new[] { "5LP" })) };

        var store = new DataStore(moves, tables);

        var table = store.GetGatlingTable("ryu");
        Assert.NotNull(table);
        var entry = Assert.Single(table!.Entries);
        Assert.Contains("5LP", entry.TargetMoves);
    }

    [Fact]
    public void OrphanSourceMove_ExcludedFromTable()
    {
        var moves = new[] { MakeMove("5MP") };
        var tables = new[] { MakeTable("ryu", MakeEntry("nonexistent", new[] { "5MP" })) };

        var store = new DataStore(moves, tables);

        var table = store.GetGatlingTable("ryu");
        Assert.NotNull(table);
        Assert.Empty(table!.Entries);
    }

    [Fact]
    public void OrphanTargetMove_ExcludedFromTargets()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5HP") };
        var tables = new[] { MakeTable("ryu", MakeEntry("5LP", new[] { "5HP", "nonexistent" })) };

        var store = new DataStore(moves, tables);

        var table = store.GetGatlingTable("ryu");
        Assert.NotNull(table);
        var entry = Assert.Single(table!.Entries);
        Assert.Equal(new[] { "5HP" }, entry.TargetMoves);
    }

    [Fact]
    public void RuntimeSwap_TakesEffectOnNextQuery()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP"), MakeMove("5HP") };
        var tableA = MakeTable("ryu", MakeEntry("5LP", new[] { "5MP" }));
        var store = new DataStore(moves, new[] { tableA });

        Assert.Equal("ryu", store.GetGatlingTable("ryu")!.CharacterId);
        Assert.Single(store.GetGatlingTable("ryu")!.Entries);

        // Swap: replace the table for the same character
        var tableB = MakeTable("ryu", MakeEntry("5LP", new[] { "5HP" }));
        var newStore = new DataStore(moves, new[] { tableB });

        var swapped = newStore.GetGatlingTable("ryu");
        Assert.NotNull(swapped);
        var entry = Assert.Single(swapped!.Entries);
        Assert.Equal(new[] { "5HP" }, entry.TargetMoves);
    }
}
