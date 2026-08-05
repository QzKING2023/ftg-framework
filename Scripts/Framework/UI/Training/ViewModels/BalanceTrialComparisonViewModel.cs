#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core.Balance;

namespace FTG_Framework.UI.Training.ViewModels;

/// <summary>One comparison row: metric, left value, right value, equality.</summary>
public sealed record BalanceComparisonRow(string Metric, string Left, string Right, bool Equal);

/// <summary>Per-frame initiation difference across two trials, with the
/// unchanged-by-design labels required by S4.1-AC02.</summary>
public sealed record BalanceInitiationComparison(
    int Frame,
    string MoveId,
    string LeftVersions,
    string RightVersions,
    bool LeftUnchangedByDesign,
    bool RightUnchangedByDesign);

/// <summary>Immutable comparison of two trial results. Comparison logic is pure
/// C# and testable without Godot (S4.1-AC10); it never mutates either result.</summary>
public sealed record BalanceTrialComparison(
    bool HashTrailsEqual,
    int? FirstDivergenceFrame,
    string? FirstDivergenceComponent,
    IReadOnlyList<BalanceComparisonRow> Rows,
    IReadOnlyList<BalanceInitiationComparison> InitiationDifferences);

/// <summary>
/// Side-by-side comparison ViewModel for balance trial results (Story 4.1).
/// Renders starting identities, tuning versions, metrics, deterministic status,
/// and differences without mutating either result (S4.1-AC10). The
/// unchanged-by-design label marks an initiation that retained its captured
/// data version across a mid-trial committed-version change (S4.1-AC02/AC09).
/// </summary>
public sealed class BalanceTrialComparisonViewModel
{
    private readonly List<BalanceTrialResult> _results = new();

    public IReadOnlyList<BalanceTrialResult> Results => _results.AsReadOnly();
    public BalanceTrialResult? Left { get; private set; }
    public BalanceTrialResult? Right { get; private set; }

    public void AddResult(BalanceTrialResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!_results.Contains(result)) _results.Add(result);
    }

    public void RemoveResult(BalanceTrialResult result)
    {
        if (_results.Remove(result))
        {
            if (ReferenceEquals(Left, result)) Left = null;
            if (ReferenceEquals(Right, result)) Right = null;
        }
    }

    public void SelectLeft(BalanceTrialResult? result)
    {
        if (result is not null && !_results.Contains(result))
            throw new ArgumentException("[Trial] Comparison selection must reference a registered trial result.", nameof(result));
        Left = result;
    }

    public void SelectRight(BalanceTrialResult? result)
    {
        if (result is not null && !_results.Contains(result))
            throw new ArgumentException("[Trial] Comparison selection must reference a registered trial result.", nameof(result));
        Right = result;
    }

    public bool CanCompare => Left is not null && Right is not null && !ReferenceEquals(Left, Right);

    public BalanceTrialComparison? Compare()
    {
        if (Left is null || Right is null || ReferenceEquals(Left, Right)) return null;
        BalanceTrialResult left = Left;
        BalanceTrialResult right = Right;

        bool hashesEqual = string.Equals(left.HashTrail, right.HashTrail, StringComparison.Ordinal);
        int? firstDivergence = hashesEqual ? null : FirstDivergenceFrame(left.HashTrail, right.HashTrail);
        string? firstDivergenceComponent = hashesEqual ? null : FirstDivergenceComponent(left.HashTrail, right.HashTrail);

        var rows = new List<BalanceComparisonRow>
        {
            new("Snapshot SHA-256", left.Binding.SnapshotSha256, right.Binding.SnapshotSha256,
                string.Equals(left.Binding.SnapshotSha256, right.Binding.SnapshotSha256, StringComparison.OrdinalIgnoreCase)),
            new("Recording", left.Binding.RecordingName, right.Binding.RecordingName,
                string.Equals(left.Binding.RecordingName, right.Binding.RecordingName, StringComparison.Ordinal)),
            new("Target player", left.Binding.TargetPlayer.ToString(), right.Binding.TargetPlayer.ToString(),
                left.Binding.TargetPlayer == right.Binding.TargetPlayer),
            new("Observation window", left.Binding.ObservationWindowFrames.ToString(), right.Binding.ObservationWindowFrames.ToString(),
                left.Binding.ObservationWindowFrames == right.Binding.ObservationWindowFrames),
            new("Reference dataset", left.Binding.ReferenceDatasetId ?? "-", right.Binding.ReferenceDatasetId ?? "-",
                string.Equals(left.Binding.ReferenceDatasetId, right.Binding.ReferenceDatasetId, StringComparison.Ordinal)),
            new("Move dataset version", left.Binding.Versions.MoveDatasetVersion.ToString(), right.Binding.Versions.MoveDatasetVersion.ToString(),
                left.Binding.Versions.MoveDatasetVersion == right.Binding.Versions.MoveDatasetVersion),
            new("Physics dataset version", left.Binding.Versions.PhysicsDatasetVersion.ToString(), right.Binding.Versions.PhysicsDatasetVersion.ToString(),
                left.Binding.Versions.PhysicsDatasetVersion == right.Binding.Versions.PhysicsDatasetVersion),
            new("Total damage", left.TotalDamage.ToString(), right.TotalDamage.ToString(),
                left.TotalDamage == right.TotalDamage),
            new("Max combo hits", left.MaxComboHits.ToString(), right.MaxComboHits.ToString(),
                left.MaxComboHits == right.MaxComboHits),
            new("Terminal move", left.TerminalMoveId ?? "-", right.TerminalMoveId ?? "-",
                string.Equals(left.TerminalMoveId, right.TerminalMoveId, StringComparison.Ordinal)),
            new("Terminal position X", left.TerminalPositionX.ToString(), right.TerminalPositionX.ToString(),
                left.TerminalPositionX == right.TerminalPositionX),
            new("Hashed frames", left.HashedFrames.ToString(), right.HashedFrames.ToString(),
                left.HashedFrames == right.HashedFrames),
            new("Per-frame hash trail (determinism)",
                hashesEqual ? "identical" : $"diverges at frame {firstDivergence ?? -1} ({firstDivergenceComponent ?? "unknown"})",
                hashesEqual ? "identical" : "differs",
                hashesEqual)
        };

        return new BalanceTrialComparison(hashesEqual, firstDivergence, firstDivergenceComponent, rows, CompareInitiations(left, right));
    }

    /// <summary>S4.1-AC06: identifies the first differing state component at the
    /// first divergent frame. Hash lines are pipe-delimited with a component
    /// prefix per segment (fd1/fd2/sm/ph/hw/cb/in — see BalanceFrameHash).</summary>
    private static string? FirstDivergenceComponent(string left, string right)
    {
        string[] a = left.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] b = right.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int common = Math.Min(a.Length, b.Length);
        for (int i = 0; i < common; i++)
        {
            if (string.Equals(a[i], b[i], StringComparison.Ordinal)) continue;
            string[] segmentsA = a[i].Split('|');
            string[] segmentsB = b[i].Split('|');
            for (int s = 1; s < Math.Min(segmentsA.Length, segmentsB.Length); s++)
            {
                if (!string.Equals(segmentsA[s], segmentsB[s], StringComparison.Ordinal))
                    return segmentsA[s].Split(':')[0];
            }
            return "structure";
        }
        return a.Length != b.Length ? "trail-length" : null;
    }

    private static int? FirstDivergenceFrame(string left, string right)
    {
        string[] a = left.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] b = right.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int common = Math.Min(a.Length, b.Length);
        for (int i = 0; i < common; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal) && TryParseFrame(a[i], out int frame))
                return frame;
        }
        if (a.Length != b.Length && TryParseFrame(a.Length > b.Length ? a[b.Length] : b[a.Length], out int extra))
            return extra;
        return null;
    }

    private static bool TryParseFrame(string hashLine, out int frame)
    {
        string? prefix = hashLine.Split('|')[0];
        return int.TryParse(prefix, out frame);
    }

    private static IReadOnlyList<BalanceInitiationComparison> CompareInitiations(
        BalanceTrialResult left, BalanceTrialResult right)
    {
        var output = new List<BalanceInitiationComparison>();
        var rightByFrame = right.Initiations.ToLookup(item => item.Frame);
        var leftByFrame = left.Initiations.ToLookup(item => item.Frame);
        foreach (BalanceInitiation leftInitiation in left.Initiations)
        {
            BalanceInitiation? rightInitiation = rightByFrame[leftInitiation.Frame].FirstOrDefault();
            bool leftUnchangedByDesign = UnchangedByDesign(left, leftInitiation);
            bool rightUnchangedByDesign = rightInitiation is { } r && UnchangedByDesign(right, r);
            if (rightInitiation is null)
            {
                output.Add(new BalanceInitiationComparison(
                    leftInitiation.Frame, leftInitiation.MoveId,
                    leftInitiation.Versions.ToString(), "-",
                    leftUnchangedByDesign, false));
                continue;
            }
            // Surface every initiation whose labeling matters, even when the raw
            // versions match: a captured-version retention is a labeling fact.
            if (string.Equals(leftInitiation.MoveId, rightInitiation.MoveId, StringComparison.Ordinal) &&
                leftInitiation.Versions == rightInitiation.Versions &&
                !leftUnchangedByDesign && !rightUnchangedByDesign) continue;
            output.Add(new BalanceInitiationComparison(
                leftInitiation.Frame,
                leftInitiation.MoveId,
                leftInitiation.Versions.ToString(),
                rightInitiation.Versions.ToString(),
                leftUnchangedByDesign,
                rightUnchangedByDesign));
        }
        // Right-only initiations (added moves or timing shifts) must be visible
        // too, otherwise a report can claim no initiation differences while the
        // initiation sets differ.
        foreach (BalanceInitiation rightInitiation in right.Initiations)
        {
            if (leftByFrame[rightInitiation.Frame].Any()) continue;
            output.Add(new BalanceInitiationComparison(
                rightInitiation.Frame, rightInitiation.MoveId,
                "-", rightInitiation.Versions.ToString(),
                false, UnchangedByDesign(right, rightInitiation)));
        }
        return output;
    }

    /// <summary>S4.1-AC02: an initiation that retained its captured data version
    /// while a later initiation in the same trial used a newer committed version
    /// was already in flight across the commit boundary — unchanged-by-design.</summary>
    private static bool UnchangedByDesign(BalanceTrialResult trial, BalanceInitiation initiation) =>
        trial.Initiations.Any(later => later.Frame > initiation.Frame &&
            (later.Versions.MoveDatasetVersion > initiation.Versions.MoveDatasetVersion ||
             later.Versions.PhysicsDatasetVersion > initiation.Versions.PhysicsDatasetVersion));
}
