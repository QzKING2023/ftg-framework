#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using Xunit;

namespace FTG_Framework.Tests.Engine.Combo;

public class ChainValidatorTests : IDisposable
{
    public ChainValidatorTests()
    {
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        EventBusTestHelper.Drain();
    }

    private static MoveDefinition MakeMove(string moveId, bool chainRepeatable = false) => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
        ChainRepeatable = chainRepeatable
    };

    private static (StubDataStore store, ChainValidator validator) Setup(
        params (string moveId, bool chainRepeatable)[] moves)
    {
        var store = new StubDataStore();
        foreach (var (moveId, chainRepeatable) in moves)
            store.SetMove(MakeMove(moveId, chainRepeatable));
        var validator = new ChainValidator(store);
        return (store, validator);
    }

    // 3.2
    [Fact]
    public void IsUnique_EmptyChain_ReturnsTrue()
    {
        var (_, validator) = Setup(
            ("5LP", false), ("5MP", false), ("5HP", false));

        Assert.True(validator.IsUnique(1, "5LP"));
    }

    // 3.3
    [Fact]
    public void IsUnique_DuplicateMove_ReturnsFalse()
    {
        var (_, validator) = Setup(
            ("5LP", false), ("5MP", false), ("5HP", false));

        validator.AddToChain(1, "5LP");
        validator.AddToChain(1, "5MP");
        validator.AddToChain(1, "5HP");

        Assert.False(validator.IsUnique(1, "5MP"));
    }

    // 3.4
    [Fact]
    public void IsUnique_UniqueMove_ReturnsTrue()
    {
        var (_, validator) = Setup(
            ("5LP", false), ("5MP", false), ("5HP", false));

        validator.AddToChain(1, "5LP");
        validator.AddToChain(1, "5MP");

        Assert.True(validator.IsUnique(1, "5HP"));
    }

    // 3.5
    [Fact]
    public void IsUnique_ChainRepeatable_AlwaysReturnsTrue()
    {
        var (_, validator) = Setup(
            ("5LP", true));

        validator.AddToChain(1, "5LP");
        Assert.True(validator.IsUnique(1, "5LP"));

        validator.AddToChain(1, "5LP");
        Assert.True(validator.IsUnique(1, "5LP"));
    }

    // 3.6
    [Fact]
    public void IsUnique_ChainRepeatableFalseDefault_ReturnsFalse()
    {
        // MoveDefinition defaults ChainRepeatable to false
        var moveDef = new MoveDefinition { MoveId = "5LP" };
        Assert.False(moveDef.ChainRepeatable);

        var store = new StubDataStore();
        store.SetMove(moveDef);
        var validator = new ChainValidator(store);

        validator.AddToChain(1, "5LP");
        Assert.False(validator.IsUnique(1, "5LP"));
    }

    // 3.7
    [Fact]
    public void ResetForPlayer_ClearsChain()
    {
        var (_, validator) = Setup(
            ("5LP", false), ("5MP", false));

        validator.AddToChain(1, "5LP");
        validator.AddToChain(1, "5MP");
        validator.ResetForPlayer(1);

        Assert.True(validator.IsUnique(1, "5LP"));
    }

    // 3.8
    [Fact]
    public void ResetForPlayer_UnknownPlayer_NoThrow()
    {
        var (_, validator) = Setup(
            ("5LP", false));

        var ex = Record.Exception(() => validator.ResetForPlayer(99));
        Assert.Null(ex);
    }

    // 3.9
    [Fact]
    public void AddToChain_NullMoveId_Throws()
    {
        var (_, validator) = Setup(
            ("5LP", false));

        Assert.Throws<ArgumentNullException>(() => validator.AddToChain(1, null!));
    }

    // 3.10
    [Fact]
    public void IsUnique_NullMoveId_Throws()
    {
        var (_, validator) = Setup(
            ("5LP", false));

        Assert.Throws<ArgumentNullException>(() => validator.IsUnique(1, null!));
    }

    // 3.11
    [Fact]
    public void IsUnique_UnknownMoveId_ReturnsTrue()
    {
        var (_, validator) = Setup(
            ("5LP", false));

        Assert.True(validator.IsUnique(1, "NONEXISTENT"));
    }

    // 3.12
    [Fact]
    public void IndependentChains_PerPlayer()
    {
        var (_, validator) = Setup(
            ("5LP", false));

        validator.AddToChain(1, "5LP");

        Assert.True(validator.IsUnique(2, "5LP"));
    }
}
