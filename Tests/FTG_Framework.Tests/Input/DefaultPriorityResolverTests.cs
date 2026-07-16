#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public class DefaultPriorityResolverTests
{
    private static StubInputLeniency CreateLeniencyWithMoves()
    {
        var leniency = new StubInputLeniency();
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "normal_jab",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward } },
            RequiredButton = ButtonValue.LP,
            Category = MoveCategory.Normal
        });
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "fireball",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "super_fireball",
            AcceptedSequences = new[]
            {
                new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward,
                        DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward }
            },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Super
        });
        return leniency;
    }

    // --- Priority sorting ---

    [Fact]
    public void Super_WinsOver_Special()
    {
        var leniency = CreateLeniencyWithMoves();
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("fireball", ButtonValue.HP, 0, 3),        // Special, seq=3
            new("super_fireball", ButtonValue.HP, 0, 6),  // Super, seq=6
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("super_fireball", result.Value.MoveId);
    }

    [Fact]
    public void Special_WinsOver_Normal()
    {
        var leniency = CreateLeniencyWithMoves();
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("normal_jab", ButtonValue.LP, 0, 1),   // Normal, seq=1
            new("fireball", ButtonValue.HP, 0, 3),     // Special, seq=3
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("fireball", result.Value.MoveId);
    }

    [Fact]
    public void LongerSequence_WinsSameCategory()
    {
        var leniency = new StubInputLeniency();
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "short_special",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "long_special",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("short_special", ButtonValue.HP, 0, 2),
            new("long_special", ButtonValue.HP, 0, 3),
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("long_special", result.Value.MoveId);
    }

    [Fact]
    public void RegistrationOrder_BreaksTie()
    {
        var leniency = new StubInputLeniency();
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "first_move",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "second_move",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("second_move", ButtonValue.HP, 0, 3),
            new("first_move", ButtonValue.HP, 0, 3),
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("first_move", result.Value.MoveId); // Lower registration index wins
    }

    // --- Charge filter ---

    [Fact]
    public void ChargeInvalidMove_Excluded()
    {
        var leniency = new StubInputLeniency();
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "sonic_boom",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            ChargeDirection = DirectionValue.Back,
            MinChargeDuration = 30,
            Category = MoveCategory.Special
        });
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "fireball",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        var chargeTracker = new StubChargeTracker { IsValidResult = false };
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("sonic_boom", ButtonValue.HP, 0, 1),
            new("fireball", ButtonValue.HP, 0, 3),
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("fireball", result.Value.MoveId); // sonic_boom filtered out
    }

    [Fact]
    public void ChargeValidMove_SurvivesFilter()
    {
        var leniency = new StubInputLeniency();
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "sonic_boom",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            ChargeDirection = DirectionValue.Back,
            MinChargeDuration = 30,
            Category = MoveCategory.Super
        });
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "fireball",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            Category = MoveCategory.Special
        });
        var chargeTracker = new StubChargeTracker { IsValidResult = true };
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("sonic_boom", ButtonValue.HP, 0, 1),
            new("fireball", ButtonValue.HP, 0, 3),
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("sonic_boom", result.Value.MoveId); // Super + valid charge = wins
    }

    [Fact]
    public void AllChargeInvalid_ReturnsNull()
    {
        var leniency = new StubInputLeniency();
        leniency.AddMove(new MoveInputConfig
        {
            MoveId = "charge_move",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward } },
            RequiredButton = ButtonValue.HP,
            ChargeDirection = DirectionValue.Back,
            MinChargeDuration = 30,
            Category = MoveCategory.Special
        });
        var chargeTracker = new StubChargeTracker { IsValidResult = false };
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("charge_move", ButtonValue.HP, 0, 1),
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.Null(result);
    }

    // --- Edge cases ---

    [Fact]
    public void EmptyCandidates_ReturnsNull()
    {
        var leniency = CreateLeniencyWithMoves();
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var result = resolver.Resolve(new List<MatchResult>(), 1, 0);
        Assert.Null(result);
    }

    [Fact]
    public void SingleCandidate_ReturnsIt()
    {
        var leniency = CreateLeniencyWithMoves();
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("fireball", ButtonValue.HP, 5, 3),
        };

        var result = resolver.Resolve(candidates, 1, 10);
        Assert.NotNull(result);
        Assert.Equal("fireball", result.Value.MoveId);
        Assert.Equal(5, result.Value.MatchedAtFrame);
        Assert.Equal(3, result.Value.SequenceLength);
    }

    [Fact]
    public void UnknownMoveId_Skipped()
    {
        var leniency = CreateLeniencyWithMoves();
        var chargeTracker = new StubChargeTracker();
        var resolver = new DefaultPriorityResolver(leniency, chargeTracker);

        var candidates = new List<MatchResult>
        {
            new("nonexistent_move", ButtonValue.HP, 0, 3),
            new("fireball", ButtonValue.HP, 0, 3),
        };

        var result = resolver.Resolve(candidates, 1, 0);
        Assert.NotNull(result);
        Assert.Equal("fireball", result.Value.MoveId);
    }
}
