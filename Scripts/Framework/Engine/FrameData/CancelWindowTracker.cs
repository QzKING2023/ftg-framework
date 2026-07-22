#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.FrameData;

internal sealed class CancelWindowTracker
{
    private IReadOnlyList<CancelWindow> _windows = new List<CancelWindow>();
    private readonly HashSet<int> _openWindows = new();
    private string _activeMoveId = string.Empty;

    public void TrackMove(int playerId, MoveDefinition move)
    {
        ArgumentNullException.ThrowIfNull(move);
        // Self-close: a replaced move's windows must not outlive it — every
        // Entered pairs with exactly one Exited even on mid-state replacement.
        CloseAll(playerId, _activeMoveId);
        _windows = move.CancelWindows;
        _activeMoveId = move.MoveId;
    }

    public void EvaluateFrame(int playerId, string moveId, int currentFrame)
    {
        for (int i = 0; i < _windows.Count; i++)
        {
            bool isOpen = _openWindows.Contains(i);
            var window = _windows[i];

            if (!isOpen && currentFrame >= window.StartFrame && currentFrame <= window.EndFrame)
            {
                _openWindows.Add(i);
                EventBus.Instance.Publish(new CancelWindowEnteredEvent(
                    playerId, moveId, window.TargetCategory, window.StartFrame, window.EndFrame));
            }
            else if (isOpen && currentFrame > window.EndFrame)
            {
                _openWindows.Remove(i);
                EventBus.Instance.Publish(new CancelWindowExitedEvent(
                    playerId, moveId, window.TargetCategory));
            }
        }
    }

    public void CloseAll(int playerId, string moveId, bool immediate = false)
    {
        foreach (int i in _openWindows.OrderBy(i => i))
        {
            var window = _windows[i];
            Emit(new CancelWindowExitedEvent(
                playerId, moveId, window.TargetCategory), immediate);
        }
        _openWindows.Clear();
    }

    // Full teardown for rewind-to-idle: closes any open windows with the tracked
    // move's id, then forgets the move entirely.
    public void Reset(int playerId, bool immediate = false)
    {
        CloseAll(playerId, _activeMoveId, immediate);
        _windows = new List<CancelWindow>();
        _activeMoveId = string.Empty;
    }

    // Reconciles tracked state with a restored frame after a rewind. Only true
    // deltas emit events — a window continuously open across the restore emits
    // nothing (unlike TrackMove + EvaluateFrame, which emits a spurious
    // Exited+Entered pair). Events dispatch immediately because ProcessFrame
    // never runs while paused.
    public void ResyncFrame(int playerId, MoveDefinition move, int currentFrame)
    {
        ArgumentNullException.ThrowIfNull(move);
        if (_activeMoveId != move.MoveId)
        {
            CloseAll(playerId, _activeMoveId, immediate: true);
            _windows = move.CancelWindows;
            _activeMoveId = move.MoveId;
        }
        for (int i = 0; i < _windows.Count; i++)
        {
            bool isOpen = _openWindows.Contains(i);
            var window = _windows[i];
            bool shouldBeOpen = currentFrame >= window.StartFrame && currentFrame <= window.EndFrame;

            if (shouldBeOpen && !isOpen)
            {
                _openWindows.Add(i);
                EventBus.Instance.PublishImmediate(new CancelWindowEnteredEvent(
                    playerId, move.MoveId, window.TargetCategory, window.StartFrame, window.EndFrame));
            }
            else if (!shouldBeOpen && isOpen)
            {
                _openWindows.Remove(i);
                EventBus.Instance.PublishImmediate(new CancelWindowExitedEvent(
                    playerId, move.MoveId, window.TargetCategory));
            }
        }
    }

    private static void Emit<T>(T evt, bool immediate) where T : struct
    {
        if (immediate)
            EventBus.Instance.PublishImmediate(evt);
        else
            EventBus.Instance.Publish(evt);
    }
}
