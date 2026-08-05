#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FTG_Framework.Core.Balance;

/// <summary>Committed data versions captured at a trial boundary (AC09/AC02).</summary>
public readonly record struct BalanceDataVersions(ulong MoveDatasetVersion, ulong PhysicsDatasetVersion)
{
    public override string ToString() => $"moves={MoveDatasetVersion},physics={PhysicsDatasetVersion}";
}

/// <summary>Developer-supplied trial specification (S4.1-AC01).</summary>
public sealed record BalanceTrialCandidate(
    string SnapshotPath,
    string RecordingName,
    int TargetPlayer,
    int ObservationWindowFrames,
    string? ExpectedSnapshotSha256 = null,
    string? ReferenceDatasetId = null);

/// <summary>Immutable trial binding: snapshot identity, recording identity, target
/// player, committed data versions, observation window, and the reference
/// dataset the trial material is drawn from (E4.1-REF-001 — P-4.1).</summary>
public sealed record BalanceTrialBinding(
    string SnapshotPath,
    string SnapshotSha256,
    string RecordingName,
    int TargetPlayer,
    BalanceDataVersions Versions,
    int ObservationWindowFrames,
    string? ReferenceDatasetId);

/// <summary>One move initiation observed during a trial, tagged with the committed
/// data versions in effect at that initiation (S4.1-AC09).</summary>
public sealed record BalanceInitiation(int Frame, string MoveId, BalanceDataVersions Versions);

/// <summary>Immutable outcome of one completed trial. Never mutated by comparison
/// logic (S4.1-AC10). Deliberately a class (reference identity): two trials with
/// identical values are still distinct results, and comparison registration must
/// not conflate them by value.</summary>
public sealed class BalanceTrialResult
{
    public BalanceTrialBinding Binding { get; }
    public int StartFrame { get; }
    public int EndFrame { get; }
    public string HashTrail { get; }
    public int TotalDamage { get; }
    public int MaxComboHits { get; }
    public string? TerminalMoveId { get; }
    public int TerminalPositionX { get; }
    public IReadOnlyList<BalanceInitiation> Initiations { get; }

    public BalanceTrialResult(
        BalanceTrialBinding binding,
        int startFrame,
        int endFrame,
        string hashTrail,
        int totalDamage,
        int maxComboHits,
        string? terminalMoveId,
        int terminalPositionX,
        IReadOnlyList<BalanceInitiation> initiations)
    {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        StartFrame = startFrame;
        EndFrame = endFrame;
        HashTrail = hashTrail ?? string.Empty;
        TotalDamage = totalDamage;
        MaxComboHits = maxComboHits;
        TerminalMoveId = terminalMoveId;
        TerminalPositionX = terminalPositionX;
        ArgumentNullException.ThrowIfNull(initiations);
        Initiations = Array.AsReadOnly(initiations.ToArray());
    }

    public int HashedFrames
    {
        get
        {
            int count = 0;
            foreach (char c in HashTrail) if (c == '\n') count++;
            return count;
        }
    }
}
