#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.UI.Training.ViewModels;

public readonly record struct ComboDisplayState(int HitCount, int TotalDamage)
{
    public static ComboDisplayState Empty { get; } = new(0, 0);
}

public sealed class ComboDisplayViewModel
{
    private readonly Dictionary<int, ComboDisplayState> _buckets = new();
    private readonly Action<string> _diagnostic;

    public ComboDisplayViewModel(Action<string>? diagnostic = null)
    {
        _diagnostic = diagnostic ?? FrameworkLog.Error;
    }

    internal int BucketCount => _buckets.Count;

    public ComboDisplayState GetState(int attackerId) =>
        _buckets.TryGetValue(attackerId, out var state) ? state : ComboDisplayState.Empty;

    public bool HandleHit(HitConnectedEvent hit)
    {
        if (!IsValidParticipantPair(hit.AttackerId, hit.DefenderId) ||
            string.IsNullOrWhiteSpace(hit.MoveId) ||
            hit.Damage < 0 ||
            hit.ContactFrame < 0)
        {
            _diagnostic($"[ComboDisplay] Invalid HitConnected payload for attacker {hit.AttackerId}.");
            return false;
        }

        var prior = GetState(hit.AttackerId);
        if (!TryAccumulate(hit.AttackerId, prior, hit.Damage, _diagnostic, out var next))
            return false;

        _buckets[hit.AttackerId] = next;
        return true;
    }

    public bool HandleBlock(MoveBlockedEvent blocked)
    {
        if (!IsValidParticipantPair(blocked.AttackerId, blocked.DefenderId) ||
            string.IsNullOrWhiteSpace(blocked.MoveId) ||
            blocked.Damage < 0 ||
            blocked.ContactFrame < 0)
        {
            _diagnostic($"[ComboDisplay] Invalid MoveBlocked payload for attacker {blocked.AttackerId}.");
            return false;
        }

        ResetAttacker(blocked.AttackerId);
        return true;
    }

    public bool HandleComboEnded(ComboEndedEvent ended)
    {
        if (ended.PlayerId <= 0 || ended.TotalHits < 0 || string.IsNullOrWhiteSpace(ended.FinalMove))
        {
            _diagnostic($"[ComboDisplay] Invalid ComboEnded payload for attacker {ended.PlayerId}.");
            return false;
        }

        ResetAttacker(ended.PlayerId);
        return true;
    }

    public void ResetAttacker(int attackerId) => _buckets.Remove(attackerId);

    public void ResetAll() => _buckets.Clear();

    internal static bool TryAccumulate(
        int attackerId,
        ComboDisplayState prior,
        int incomingDamage,
        Action<string> diagnostic,
        out ComboDisplayState result)
    {
        try
        {
            int hitCount = checked(prior.HitCount + 1);
            int totalDamage = checked(prior.TotalDamage + incomingDamage);
            result = new ComboDisplayState(hitCount, totalDamage);
            return true;
        }
        catch (OverflowException)
        {
            result = prior;
            diagnostic(
                $"[ComboDisplay] Overflow rejected for attacker {attackerId}; " +
                $"attempted count {prior.HitCount} + 1, damage {prior.TotalDamage} + {incomingDamage}.");
            return false;
        }
    }

    private static bool IsValidParticipantPair(int attackerId, int defenderId) =>
        attackerId > 0 && defenderId > 0 && attackerId != defenderId;
}
