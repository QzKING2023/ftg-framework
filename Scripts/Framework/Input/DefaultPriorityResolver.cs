#nullable enable
using System;
using System.Collections.Generic;

using FTG_Framework.Core;
using Godot;

namespace FTG_Framework.Input;

internal sealed class DefaultPriorityResolver : IModule, IPriorityResolver
{
    private readonly IInputLeniency _leniencyMatcher;
    private readonly IChargeTracker _chargeTracker;

    public DefaultPriorityResolver(IInputLeniency leniencyMatcher, IChargeTracker chargeTracker)
    {
        ArgumentNullException.ThrowIfNull(leniencyMatcher);
        ArgumentNullException.ThrowIfNull(chargeTracker);
        _leniencyMatcher = leniencyMatcher;
        _chargeTracker = chargeTracker;
    }

    public void Initialize(IDataStore dataStore)
    {
        GD.Print("[Input] DefaultPriorityResolver initialized.");
    }

    public void Shutdown()
    {
    }

    public MatchResult? Resolve(IReadOnlyList<MatchResult> candidates, int playerId, int currentFrame)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return null;
        }

        if (candidates is null || candidates.Count == 0)
            return null;

        var moveConfigs = _leniencyMatcher.GetRegisteredMoves();

        // Phase 1: Filter by charge validity
        var eligible = new List<(MatchResult Match, MoveCategory Category, int RegistrationIndex)>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var config = FindConfig(candidate.MoveId, moveConfigs);
            if (config is null)
                continue;

            if (config.ChargeDirection != null)
            {
                if (!_chargeTracker.IsChargeValid(playerId, config.ChargeDirection.Value,
                        config.MinChargeDuration, currentFrame))
                    continue;
            }

            int regIndex = IndexOfConfig(candidate.MoveId, moveConfigs);
            eligible.Add((candidate, config.Category, regIndex));
        }

        if (eligible.Count == 0)
            return null;

        // Phase 2: Sort by priority (descending)
        eligible.Sort((a, b) =>
        {
            // Higher category wins
            int cmp = ((int)b.Category).CompareTo((int)a.Category);
            if (cmp != 0)
                return cmp;

            // Longer sequence wins
            cmp = b.Match.SequenceLength.CompareTo(a.Match.SequenceLength);
            if (cmp != 0)
                return cmp;

            // Registration order (lower index wins)
            return a.RegistrationIndex.CompareTo(b.RegistrationIndex);
        });

        return eligible[0].Match;
    }

    private static MoveInputConfig? FindConfig(string moveId, IReadOnlyList<MoveInputConfig> configs)
    {
        for (int i = 0; i < configs.Count; i++)
        {
            if (configs[i].MoveId == moveId)
                return configs[i];
        }
        return null;
    }

    private static int IndexOfConfig(string moveId, IReadOnlyList<MoveInputConfig> configs)
    {
        for (int i = 0; i < configs.Count; i++)
        {
            if (configs[i].MoveId == moveId)
                return i;
        }
        return int.MaxValue;
    }
}
