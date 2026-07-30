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
        return TryMatch(playerId, 0, int.MaxValue);
    }

    public IReadOnlyList<MatchResult> TryMatch(int playerId, int fromFrame, int toFrame)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return Array.Empty<MatchResult>();
        }

        var history = _inputHistory.GetDirectionalHistory(playerId);
        if (history.Count == 0)
            return Array.Empty<MatchResult>();

        var windowedHistory = new List<InputEntry>();
        for (int i = 0; i < history.Count; i++)
        {
            var entry = history[i];
            if (entry.Frame >= fromFrame && entry.Frame <= toFrame)
                windowedHistory.Add(entry);
        }

        if (windowedHistory.Count == 0)
            return Array.Empty<MatchResult>();

        var results = new List<MatchResult>();
        foreach (var config in _registeredMoves)
        {
            MatchResult? latestMatch = null;
            foreach (var sequence in config.AcceptedSequences)
            {
                if (TryMatchSequence(sequence, windowedHistory, out int lastFrame))
                {
                    var match = new MatchResult(
                        config.MoveId, config.RequiredButton, lastFrame, sequence.Length);
                    if (latestMatch is null ||
                        match.MatchedAtFrame > latestMatch.Value.MatchedAtFrame)
                    {
                        latestMatch = match;
                    }
                }
            }
            if (latestMatch is { } best)
                results.Add(best);
        }
        return results;
    }

    private static bool TryMatchSequence(
        DirectionValue[] sequence,
        IReadOnlyList<InputEntry> history,
        out int lastFrame)
    {
        lastFrame = 0;

        // Prefer the newest possible completion, then validate preceding sequence
        // elements backwards. This retains ordered-subsequence leniency while making
        // MatchedAtFrame represent the latest complete input in the requested window.
        for (int completion = history.Count - 1; completion >= 0; completion--)
        {
            if (history[completion].Value != (int)sequence[^1])
                continue;

            int sequenceIndex = sequence.Length - 2;
            for (int historyIndex = completion - 1;
                 historyIndex >= 0 && sequenceIndex >= 0;
                 historyIndex--)
            {
                if (history[historyIndex].Value == (int)sequence[sequenceIndex])
                    sequenceIndex--;
            }

            if (sequenceIndex >= 0)
                continue;

            lastFrame = history[completion].Frame;
            return true;
        }

        return false;
    }
}
