#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public class InputLeniencyMatcherTests
{
    private static (InputLeniencyMatcher, StubInputHistory) CreateMatcher(
        StubInputHistory? history = null,
        params MoveInputConfig[] moves)
    {
        var h = history ?? new StubInputHistory();
        var matcher = new InputLeniencyMatcher(h);
        foreach (var move in moves)
            matcher.RegisterMove(move);
        return (matcher, h);
    }

    [Fact]
    public void TryMatch_ExactSequence_ReturnsMatch()
    {
        var history = new StubInputHistory();
        history.AddDirectionalEntry(1, 0, DirectionValue.Forward);
        history.AddDirectionalEntry(1, 1, DirectionValue.Down);
        history.AddDirectionalEntry(1, 2, DirectionValue.DownForward);
        var (matcher, _) = CreateMatcher(history, new MoveInputConfig
        {
            MoveId = "dp_c",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward } },
            RequiredButton = ButtonValue.C,
            Category = MoveCategory.Special
        });

        var results = matcher.TryMatch(1, 0, 10);
        Assert.Single(results);
        Assert.Equal("dp_c", results[0].MoveId);
        Assert.Equal(2, results[0].MatchedAtFrame);
        Assert.Equal(3, results[0].SequenceLength);
    }

    [Fact]
    public void TryMatch_SequenceWithNeutralSkip_ReturnsMatch()
    {
        var history = new StubInputHistory();
        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        history.AddDirectionalEntry(1, 1, DirectionValue.Neutral);
        history.AddDirectionalEntry(1, 2, DirectionValue.DownForward);
        history.AddDirectionalEntry(1, 3, DirectionValue.Neutral);
        history.AddDirectionalEntry(1, 4, DirectionValue.Forward);
        var (matcher, _) = CreateMatcher(history, new MoveInputConfig
        {
            MoveId = "fireball_c",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.C,
            Category = MoveCategory.Special
        });

        var results = matcher.TryMatch(1, 0, 10);
        Assert.Single(results);
        Assert.Equal("fireball_c", results[0].MoveId);
        Assert.Equal(4, results[0].MatchedAtFrame);
    }

    [Fact]
    public void TryMatch_NonMatchingHistory_ReturnsEmpty()
    {
        var history = new StubInputHistory();
        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        history.AddDirectionalEntry(1, 1, DirectionValue.DownForward);
        history.AddDirectionalEntry(1, 2, DirectionValue.Forward);
        var (matcher, _) = CreateMatcher(history, new MoveInputConfig
        {
            MoveId = "dp_c",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward } },
            RequiredButton = ButtonValue.C,
            Category = MoveCategory.Special
        });

        var results = matcher.TryMatch(1, 0, 10);
        Assert.Empty(results);
    }

    [Fact]
    public void TryMatch_PartialMatch_ReturnsEmpty()
    {
        var history = new StubInputHistory();
        history.AddDirectionalEntry(1, 0, DirectionValue.Forward);
        history.AddDirectionalEntry(1, 1, DirectionValue.Down);
        var (matcher, _) = CreateMatcher(history, new MoveInputConfig
        {
            MoveId = "dp_c",
            AcceptedSequences = new[] { new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward } },
            RequiredButton = ButtonValue.C,
            Category = MoveCategory.Special
        });

        var results = matcher.TryMatch(1, 0, 10);
        Assert.Empty(results);
    }

    [Fact]
    public void TryMatch_MultipleSequences_MatchesFirstAccepted()
    {
        var history = new StubInputHistory();
        history.AddDirectionalEntry(1, 0, DirectionValue.DownForward);
        history.AddDirectionalEntry(1, 1, DirectionValue.Down);
        history.AddDirectionalEntry(1, 2, DirectionValue.DownForward);
        var (matcher, _) = CreateMatcher(history, new MoveInputConfig
        {
            MoveId = "dp_c",
            AcceptedSequences = new[]
            {
                new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward },
                new[] { DirectionValue.DownForward, DirectionValue.Down, DirectionValue.DownForward }
            },
            RequiredButton = ButtonValue.C,
            Category = MoveCategory.Special
        });

        var results = matcher.TryMatch(1, 0, 10);
        // First sequence [F,D,DF] doesn't match (first entry is DF).
        // Second sequence [DF,D,DF] matches at frame 2.
        Assert.Single(results);
        Assert.Equal("dp_c", results[0].MoveId);
        Assert.Equal(2, results[0].MatchedAtFrame);
    }

    [Fact]
    public void TryMatch_RespectsFrameWindow()
    {
        var history = new StubInputHistory();
        history.AddDirectionalEntry(1, 0, DirectionValue.Down);
        history.AddDirectionalEntry(1, 1, DirectionValue.DownForward);
        history.AddDirectionalEntry(1, 2, DirectionValue.Forward);
        history.AddDirectionalEntry(1, 10, DirectionValue.Down);
        history.AddDirectionalEntry(1, 11, DirectionValue.DownForward);
        history.AddDirectionalEntry(1, 12, DirectionValue.Forward);
        var (matcher, _) = CreateMatcher(history, new MoveInputConfig
        {
            MoveId = "fireball_c",
            AcceptedSequences = new[] { new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward } },
            RequiredButton = ButtonValue.C,
            Category = MoveCategory.Special
        });

        // Only the second set (frames 10-12) should match.
        var results = matcher.TryMatch(1, 8, 15);
        Assert.Single(results);
        Assert.Equal(12, results[0].MatchedAtFrame);
    }
}
