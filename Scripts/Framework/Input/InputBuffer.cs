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
    private readonly int _motionWindow;

    public int BufferDuration => _bufferDuration;
    public int MotionWindow => _motionWindow;

    public InputBuffer(IInputHistory inputHistory, IInputLeniency leniencyMatcher, int bufferDuration = 6, int motionWindow = 30)
    {
        ArgumentNullException.ThrowIfNull(inputHistory);
        ArgumentNullException.ThrowIfNull(leniencyMatcher);
        if (bufferDuration < 0)
            throw new ArgumentException($"[Input] Buffer duration must be >= 0, got {bufferDuration}");
        if (motionWindow < 0)
            throw new ArgumentException($"[Input] Motion window must be >= 0, got {motionWindow}");
        _inputHistory = inputHistory;
        _leniencyMatcher = leniencyMatcher;
        _bufferDuration = bufferDuration;
        _motionWindow = motionWindow;
    }

    public void Initialize(IDataStore dataStore)
    {
        GD.Print($"[Input] InputBuffer initialized — buffer: {_bufferDuration}f, motion: {_motionWindow}f.");
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
        int motionFrom = Math.Max(0, currentFrame - _motionWindow);
        int bufferFrom = Math.Max(0, currentFrame - _bufferDuration);

        var directionMatches = _leniencyMatcher.TryMatch(playerId, motionFrom, currentFrame);
        if (directionMatches.Count == 0)
            return Array.Empty<MatchResult>();

        var buttonHistory = _inputHistory.GetButtonHistory(playerId);
        var results = new List<MatchResult>();
        foreach (var match in directionMatches)
        {
            bool isNeutralNormal =
                match.SequenceLength == 1 &&
                match.RequiredButton is ButtonValue.A or ButtonValue.B;
            if (isNeutralNormal &&
                (match.MatchedAtFrame != currentFrame ||
                 !ButtonExistsAtFrame(buttonHistory, match.RequiredButton, currentFrame)))
                continue;

            if (ButtonExistsInWindow(buttonHistory, match.RequiredButton, bufferFrom, currentFrame))
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

    private static bool ButtonExistsAtFrame(
        IReadOnlyList<InputEntry> buttonHistory,
        ButtonValue requiredButton,
        int frame)
    {
        for (int i = 0; i < buttonHistory.Count; i++)
        {
            var entry = buttonHistory[i];
            if (entry.Frame == frame && entry.Value == (int)requiredButton)
                return true;
        }
        return false;
    }
}
