#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using Godot;

namespace FTG_Framework.Input;

internal sealed class InputLeniencyMatcher : IModule, IInputLeniency
{
    private readonly IInputHistory _inputHistory;
    private readonly List<MoveInputConfig> _registeredMoves = new();

    public InputLeniencyMatcher(IInputHistory inputHistory)
    {
        ArgumentNullException.ThrowIfNull(inputHistory);
        _inputHistory = inputHistory;
    }

    public void Initialize(IDataStore dataStore)
    {
        GD.Print($"[Input] InputLeniencyMatcher initialized — {_registeredMoves.Count} moves registered.");
    }

    public void Shutdown()
    {
    }

    public void RegisterMove(MoveInputConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrEmpty(config.MoveId))
            throw new ArgumentException("[Input] MoveInputConfig.MoveId must not be null or empty.");

        if (config.AcceptedSequences is null || config.AcceptedSequences.Length == 0)
            throw new ArgumentException(
                $"[Input] Move '{config.MoveId}': AcceptedSequences must contain at least one sequence.");

        for (int i = 0; i < config.AcceptedSequences.Length; i++)
        {
            if (config.AcceptedSequences[i] is null || config.AcceptedSequences[i].Length == 0)
                throw new ArgumentException(
                    $"[Input] Move '{config.MoveId}': AcceptedSequences[{i}] must not be null or empty.");
        }

        foreach (var existing in _registeredMoves)
        {
            if (existing.MoveId == config.MoveId)
                throw new ArgumentException(
                    $"[Input] Duplicate MoveId: '{config.MoveId}' is already registered.");
        }

        _registeredMoves.Add(config);
    }

    public IReadOnlyList<MoveInputConfig> GetRegisteredMoves()
    {
        return _registeredMoves.AsReadOnly();
    }

    public IReadOnlyList<MatchResult> TryMatch(int playerId)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return Array.Empty<MatchResult>();
        }

        var history = _inputHistory.GetDirectionalHistory(playerId);
        if (history.Count == 0)
            return Array.Empty<MatchResult>();

        var results = new List<MatchResult>();
        foreach (var config in _registeredMoves)
        {
            foreach (var sequence in config.AcceptedSequences)
            {
                if (TryMatchSequence(sequence, history, out int lastFrame))
                {
                    results.Add(new MatchResult(config.MoveId, config.RequiredButton, lastFrame));
                    break;
                }
            }
        }
        return results;
    }

    private static bool TryMatchSequence(
        DirectionValue[] sequence,
        IReadOnlyList<InputEntry> history,
        out int lastFrame)
    {
        int seqIdx = 0;
        lastFrame = 0;

        for (int i = 0; i < history.Count; i++)
        {
            if ((int)sequence[seqIdx] == history[i].Value)
            {
                lastFrame = history[i].Frame;
                seqIdx++;
                if (seqIdx == sequence.Length)
                    return true;
            }
        }
        return false;
    }
}
