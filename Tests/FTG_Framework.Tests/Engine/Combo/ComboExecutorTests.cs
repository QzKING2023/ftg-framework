#nullable enable
using System.Collections.Generic;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using Xunit;

namespace FTG_Framework.Tests;

public class ComboExecutorTests
{
    private static MoveDefinition MakeMove(string moveId, string category = "normal") => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
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
    public void CanCancel_ValidTarget_ReturnsTrue()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP"), MakeMove("5HP") };
        var store = new StubDataStore();
        foreach (var m in moves) store.SetMove(m);
        store.SetGatlingTable(MakeTable("ryu", MakeEntry("5LP", new[] { "5MP", "5HP" })));

        var executor = new ComboExecutor(store);

        Assert.True(executor.CanCancel("ryu", "5LP", "5MP", "normal"));
    }

    [Fact]
    public void CanCancel_WrongCategory_ReturnsFalse()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP") };
        var store = new StubDataStore();
        foreach (var m in moves) store.SetMove(m);
        store.SetGatlingTable(MakeTable("ryu", MakeEntry("5LP", new[] { "5MP" }, "normal")));

        var executor = new ComboExecutor(store);

        Assert.False(executor.CanCancel("ryu", "5LP", "5MP", "special"));
    }

    [Fact]
    public void CanCancel_TargetNotInList_ReturnsFalse()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP"), MakeMove("5HK") };
        var store = new StubDataStore();
        foreach (var m in moves) store.SetMove(m);
        store.SetGatlingTable(MakeTable("ryu", MakeEntry("5LP", new[] { "5MP" })));

        var executor = new ComboExecutor(store);

        Assert.False(executor.CanCancel("ryu", "5LP", "5HK", "normal"));
    }

    [Fact]
    public void CanCancel_SourceNotInTable_ReturnsFalse()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP") };
        var store = new StubDataStore();
        foreach (var m in moves) store.SetMove(m);
        store.SetGatlingTable(MakeTable("ryu", MakeEntry("5LP", new[] { "5MP" })));

        var executor = new ComboExecutor(store);

        Assert.False(executor.CanCancel("ryu", "5HK", "5MP", "normal"));
    }

    [Fact]
    public void CanCancel_UnknownCharacter_ReturnsFalse()
    {
        var store = new StubDataStore();

        var executor = new ComboExecutor(store);

        Assert.False(executor.CanCancel("unregistered", "5LP", "5MP", "normal"));
    }

    [Fact]
    public void CanCancel_RuntimeSwap_ReflectedImmediately()
    {
        var moves = new[] { MakeMove("5LP"), MakeMove("5MP"), MakeMove("5HP") };
        var store = new StubDataStore();
        foreach (var m in moves) store.SetMove(m);
        store.SetGatlingTable(MakeTable("ryu", MakeEntry("5LP", new[] { "5MP" })));

        var executor = new ComboExecutor(store);

        Assert.True(executor.CanCancel("ryu", "5LP", "5MP", "normal"));
        Assert.False(executor.CanCancel("ryu", "5LP", "5HP", "normal"));

        // Swap table at runtime
        store.SetGatlingTable(MakeTable("ryu", MakeEntry("5LP", new[] { "5HP" })));

        Assert.False(executor.CanCancel("ryu", "5LP", "5MP", "normal"));
        Assert.True(executor.CanCancel("ryu", "5LP", "5HP", "normal"));
    }
}
