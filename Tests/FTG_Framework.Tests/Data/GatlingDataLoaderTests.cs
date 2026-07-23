#nullable enable
using System;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public class GatlingDataLoaderTests
{
    [Fact]
    public void LoadFromJson_ValidTable_ParsesCorrectly()
    {
        var json = @"{
            ""gatling_tables"": [
                {
                    ""character_id"": ""ryu"",
                    ""entries"": [
                        {
                            ""source_move"": ""5LP"",
                            ""target_moves"": [""5MP"", ""5HP""],
                            ""cancel_category"": ""normal""
                        }
                    ]
                }
            ]
        }";

        var tables = GatlingDataLoader.LoadFromJson(json);

        var table = Assert.Single(tables);
        Assert.Equal("ryu", table.CharacterId);

        var entry = Assert.Single(table.Entries);
        Assert.Equal("5LP", entry.SourceMove);
        Assert.Equal(new[] { "5MP", "5HP" }, entry.TargetMoves);
        Assert.Equal("normal", entry.CancelCategory);
    }

    [Fact]
    public void LoadFromJson_MultipleTables_ParsesAll()
    {
        var json = @"{
            ""gatling_tables"": [
                { ""character_id"": ""ryu"", ""entries"": [{ ""source_move"": ""5LP"", ""target_moves"": [""5MP""], ""cancel_category"": ""normal"" }] },
                { ""character_id"": ""ken"", ""entries"": [{ ""source_move"": ""5LP"", ""target_moves"": [""5HK""], ""cancel_category"": ""normal"" }] }
            ]
        }";

        var tables = GatlingDataLoader.LoadFromJson(json);

        Assert.Equal(2, tables.Length);
        Assert.Contains(tables, t => t.CharacterId == "ryu");
        Assert.Contains(tables, t => t.CharacterId == "ken");
    }

    [Fact]
    public void LoadFromJson_EmptyEntries_IsValid()
    {
        var json = @"{
            ""gatling_tables"": [
                { ""character_id"": ""ryu"", ""entries"": [] }
            ]
        }";

        var tables = GatlingDataLoader.LoadFromJson(json);

        var table = Assert.Single(tables);
        Assert.Equal("ryu", table.CharacterId);
        Assert.Empty(table.Entries);
    }

    [Fact]
    public void LoadFromJson_MissingCharacterId_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                { ""character_id"": """", ""entries"": [] }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
        Assert.Contains("[Data]", ex.Message);
        Assert.Contains("character_id", ex.Message);
    }

    [Fact]
    public void LoadFromJson_EmptySourceMove_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                {
                    ""character_id"": ""ryu"",
                    ""entries"": [
                        { ""source_move"": """", ""target_moves"": [""5MP""], ""cancel_category"": ""normal"" }
                    ]
                }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
        Assert.Contains("source_move", ex.Message);
    }

    [Fact]
    public void LoadFromJson_EmptyTargetMoves_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                {
                    ""character_id"": ""ryu"",
                    ""entries"": [
                        { ""source_move"": ""5LP"", ""target_moves"": [], ""cancel_category"": ""normal"" }
                    ]
                }
            ]
        }";

        Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_EmptyTargetMoveId_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                {
                    ""character_id"": ""ryu"",
                    ""entries"": [
                        { ""source_move"": ""5LP"", ""target_moves"": [""""], ""cancel_category"": ""normal"" }
                    ]
                }
            ]
        }";

        Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_EmptyCancelCategory_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                {
                    ""character_id"": ""ryu"",
                    ""entries"": [
                        { ""source_move"": ""5LP"", ""target_moves"": [""5MP""], ""cancel_category"": """" }
                    ]
                }
            ]
        }";

        Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
    }

    [Fact]
    public void LoadFromJson_DuplicateCharacterId_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                { ""character_id"": ""ryu"", ""entries"": [{ ""source_move"": ""5LP"", ""target_moves"": [""5MP""], ""cancel_category"": ""normal"" }] },
                { ""character_id"": ""ryu"", ""entries"": [{ ""source_move"": ""5HP"", ""target_moves"": [""236P""], ""cancel_category"": ""special"" }] }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("ryu", ex.Message);
    }

    [Fact]
    public void LoadFromJson_DuplicateEntry_ThrowsFormatException()
    {
        var json = @"{
            ""gatling_tables"": [
                {
                    ""character_id"": ""ryu"",
                    ""entries"": [
                        { ""source_move"": ""5LP"", ""target_moves"": [""5MP"", ""5HP""], ""cancel_category"": ""normal"" },
                        { ""source_move"": ""5LP"", ""target_moves"": [""5HP""], ""cancel_category"": ""normal"" }
                    ]
                }
            ]
        }";

        var ex = Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
        Assert.Contains("duplicate", ex.Message.ToLower());
    }

    [Fact]
    public void LoadFromJson_MissingMovesArray_ThrowsFormatException()
    {
        var json = @"{ ""gatling_tables"": [] }";

        var ex = Assert.Throws<FormatException>(() => GatlingDataLoader.LoadFromJson(json));
        Assert.Contains("[Data]", ex.Message);
    }
}
