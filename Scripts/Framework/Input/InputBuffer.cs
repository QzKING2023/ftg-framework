#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using Godot;

namespace FTG_Framework.Input;

internal sealed class InputBuffer : IModule, IInputBuffer
{
    private readonly IInputHistory _inputHistory;
    private readonly IInputLeniency _leniencyMatcher;
    private readonly int _bufferDuration;

    public int BufferDuration => _bufferDuration;

    public InputBuffer(IInputHistory inputHistory, IInputLeniency leniencyMatcher, int bufferDuration = 6)
    {
        ArgumentNullException.ThrowIfNull(inputHistory);
        ArgumentNullException.ThrowIfNull(leniencyMatcher);
        if (bufferDuration < 0)
            throw new ArgumentException($"[Input] Buffer duration must be >= 0, got {bufferDuration}");
        _inputHistory = inputHistory;
        _leniencyMatcher = leniencyMatcher;
        _bufferDuration = bufferDuration;
    }

    public void Initialize(IDataStore dataStore)
    {
        GD.Print($"[Input] InputBuffer initialized — buffer: {_bufferDuration}f.");
    }

    public void Shutdown()
    {
    }

    public IReadOnlyList<MatchResult> TryMatch(int playerId)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return Array.Empty<MatchResult>();
        }

        int currentFrame = EventBus.Instance.CurrentFrame;
        int fromFrame = Math.Max(0, currentFrame - _bufferDuration);

        var directionMatches = _leniencyMatcher.TryMatch(playerId, fromFrame, currentFrame);
        if (directionMatches.Count == 0)
            return Array.Empty<MatchResult>();

        var buttonHistory = _inputHistory.GetButtonHistory(playerId);
        var results = new List<MatchResult>();
        foreach (var match in directionMatches)
        {
            if (ButtonExistsInWindow(buttonHistory, match.RequiredButton, fromFrame, currentFrame))
                results.Add(match);
        }

        return results;
    }

    private static bool ButtonExistsInWindow(
        IReadOnlyList<InputEntry> buttonHistory,
        ButtonValue requiredButton,
        int fromFrame,
        int toFrame)
    {
        for (int i = 0; i < buttonHistory.Count; i++)
        {
            var entry = buttonHistory[i];
            if (entry.Frame >= fromFrame && entry.Frame <= toFrame && entry.Value == (int)requiredButton)
                return true;
        }
        return false;
    }
}
