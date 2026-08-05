#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FTG_Framework.Core.Balance;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public sealed class BalanceTrialComparisonViewModelTests
{
    private static BalanceTrialBinding Binding(ulong moveVersion = 2, ulong physicsVersion = 1,
        string? referenceDatasetId = "E4.1-REF-001") =>
        new("snapshot.json", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "rtrial", 1, new BalanceDataVersions(moveVersion, physicsVersion), 40, referenceDatasetId);

    private static BalanceTrialResult Result(
        string hashTrail,
        BalanceTrialBinding? binding = null,
        int totalDamage = 30,
        int maxComboHits = 3,
        string? terminalMoveId = "5A",
        int terminalPositionX = 40,
        params BalanceInitiation[] initiations) =>
        new(binding ?? Binding(), 1, 40, hashTrail, totalDamage, maxComboHits,
            terminalMoveId, terminalPositionX,
            initiations.Length == 0
                ? new[] { new BalanceInitiation(1, "5A", new BalanceDataVersions(2, 1)) }
                : initiations);

    private static string Trail(params string[] lines) => string.Join('\n', lines) + "\n";

    [Fact]
    public void Compare_IdenticalResults_ReportsDeterministicAndEqualMetrics()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var trail = Trail("1|A", "2|B", "3|C");
        var left = Result(trail);
        var right = Result(trail);
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        Assert.True(comparison!.HashTrailsEqual);
        Assert.Null(comparison.FirstDivergenceFrame);
        Assert.Empty(comparison.InitiationDifferences);
        Assert.All(comparison.Rows, row => Assert.True(row.Equal));
    }

    [Fact]
    public void Compare_DifferentHashTrails_ReportsFirstDivergenceFrame()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A", "2|B", "3|C"));
        var right = Result(Trail("1|A", "2|X", "3|C"));
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        Assert.False(comparison!.HashTrailsEqual);
        Assert.Equal(2, comparison.FirstDivergenceFrame);
    }

    [Fact]
    public void Compare_DifferentMetrics_ReportsAttributableDifferences()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A", "2|B"), totalDamage: 30, maxComboHits: 3, terminalPositionX: 40);
        var right = Result(Trail("1|A", "2|B"), totalDamage: 36, maxComboHits: 4, terminalPositionX: 52);
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        Assert.True(comparison!.HashTrailsEqual, "identical input schedule and hashes");
        var damageRow = Assert.Single(comparison.Rows, row => row.Metric == "Total damage");
        Assert.False(damageRow.Equal);
        Assert.Equal("30", damageRow.Left);
        Assert.Equal("36", damageRow.Right);
        var comboRow = Assert.Single(comparison.Rows, row => row.Metric == "Max combo hits");
        Assert.False(comboRow.Equal);
        var positionRow = Assert.Single(comparison.Rows, row => row.Metric == "Terminal position X");
        Assert.False(positionRow.Equal);
    }

    [Fact]
    public void Compare_InitiationVersionRetention_IsLabeledUnchangedByDesign()
    {
        // Right trial: the snapshot restore ran under committed version 2, but the
        // move at frame 1 was initiated before a mid-trial commit to version 3 —
        // it retained its captured version (AC02/AC09) and must be labeled
        // unchanged-by-design, never evidence that version 3 failed to apply.
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A", "2|B"),
            binding: Binding(moveVersion: 2),
            initiations: new[]
            {
                new BalanceInitiation(1, "5A", new BalanceDataVersions(2, 1)),
                new BalanceInitiation(6, "5A", new BalanceDataVersions(2, 1))
            });
        var right = Result(Trail("1|A", "2|B"),
            binding: Binding(moveVersion: 3),
            initiations: new[]
            {
                new BalanceInitiation(1, "5A", new BalanceDataVersions(2, 1)),
                new BalanceInitiation(6, "5A", new BalanceDataVersions(3, 1))
            });
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        var frame1 = Assert.Single(comparison!.InitiationDifferences, difference => difference.Frame == 1);
        Assert.False(frame1.LeftUnchangedByDesign);
        Assert.True(frame1.RightUnchangedByDesign,
            "initiation that retained the pre-commit captured version is unchanged-by-design");
        var frame6 = Assert.Single(comparison.InitiationDifferences, difference => difference.Frame == 6);
        Assert.Equal("moves=3,physics=1", frame6.RightVersions);
        Assert.False(frame6.RightUnchangedByDesign);
    }

    [Fact]
    public void Compare_RequiresTwoDistinctRegisteredResults()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var single = Result(Trail("1|A"));
        viewModel.AddResult(single);

        Assert.False(viewModel.CanCompare);
        Assert.Null(viewModel.Compare());

        viewModel.SelectLeft(single);
        viewModel.SelectRight(single);
        Assert.False(viewModel.CanCompare);
        Assert.Null(viewModel.Compare());

        var other = Result(Trail("1|A"));
        Assert.Throws<System.ArgumentException>(() => viewModel.SelectLeft(other));
    }

    [Fact]
    public void RemoveResult_ClearsSelectionAndResults()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A"));
        var right = Result(Trail("1|A"));
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        viewModel.RemoveResult(left);

        Assert.Null(viewModel.Left);
        Assert.Same(right, viewModel.Right);
        Assert.False(viewModel.CanCompare);
        Assert.Single(viewModel.Results);
    }

    [Fact]
    public void Compare_Rows_IncludeIdentitiesVersionsMetricsAndDeterminism()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A", "2|B"));
        var right = Result(Trail("1|A", "2|B"));
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        string[] expectedMetrics =
        {
            "Snapshot SHA-256", "Recording", "Target player", "Observation window",
            "Reference dataset",
            "Move dataset version", "Physics dataset version",
            "Total damage", "Max combo hits", "Terminal move", "Terminal position X",
            "Hashed frames", "Per-frame hash trail (determinism)"
        };
        foreach (string metric in expectedMetrics)
            Assert.Contains(comparison!.Rows, row => row.Metric == metric);
    }

    [Fact]
    public void Compare_DoesNotMutateEitherResult()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A", "2|B"), totalDamage: 30);
        var right = Result(Trail("1|A", "2|X"), totalDamage: 36);
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        _ = viewModel.Compare();
        _ = viewModel.Compare();

        Assert.Equal(30, left.TotalDamage);
        Assert.Equal(36, right.TotalDamage);
        Assert.Equal(Trail("1|A", "2|B"), left.HashTrail);
        Assert.Equal(Trail("1|A", "2|X"), right.HashTrail);
        Assert.Single(left.Initiations);
        Assert.Single(right.Initiations);
    }

    [Fact]
    public void Compare_SameFrameInitiations_DoNotThrow()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var initiations = new[]
        {
            new BalanceInitiation(5, "5LP", new BalanceDataVersions(2, 1)),
            new BalanceInitiation(5, "5HP", new BalanceDataVersions(2, 1)),
            new BalanceInitiation(8, "236P", new BalanceDataVersions(2, 1))
        };
        var left = Result(Trail("1|A", "2|B"), initiations: initiations);
        var right = Result(Trail("1|A", "2|B"), initiations: initiations);
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        // Two initiations at the same dispatch frame must not throw in the
        // comparison (regression for the ToDictionary duplicate-key crash).
        var comparison = viewModel.Compare();
        Assert.NotNull(comparison);
        Assert.True(comparison!.HashTrailsEqual);
    }

    [Fact]
    public void Compare_RightOnlyInitiation_IsSurfaced()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|A", "2|B"),
            initiations: new[] { new BalanceInitiation(5, "5LP", new BalanceDataVersions(2, 1)) });
        var right = Result(Trail("1|A", "2|B"),
            initiations: new[]
            {
                new BalanceInitiation(5, "5LP", new BalanceDataVersions(2, 1)),
                new BalanceInitiation(8, "236P", new BalanceDataVersions(2, 1))
            });
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        Assert.Contains(comparison!.InitiationDifferences,
            diff => diff.Frame == 8 && diff.MoveId == "236P" && diff.LeftVersions == "-");
    }

    [Fact]
    public void Compare_DifferentHashTrails_ReportsDivergenceComponent()
    {
        var viewModel = new BalanceTrialComparisonViewModel();
        var left = Result(Trail("1|fd1:A|fd2:B", "2|fd1:A|fd2:B"));
        var right = Result(Trail("1|fd1:A|fd2:B", "2|fd1:X|fd2:B"));
        viewModel.AddResult(left);
        viewModel.AddResult(right);
        viewModel.SelectLeft(left);
        viewModel.SelectRight(right);

        var comparison = viewModel.Compare();

        Assert.NotNull(comparison);
        Assert.False(comparison!.HashTrailsEqual);
        Assert.Equal(2, comparison.FirstDivergenceFrame);
        Assert.Equal("fd1", comparison.FirstDivergenceComponent);
    }
}
