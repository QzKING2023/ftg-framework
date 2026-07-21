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

    public void CloseAll(int playerId, string moveId)
    {
        foreach (int i in _openWindows.OrderBy(i => i))
        {
            var window = _windows[i];
            EventBus.Instance.Publish(new CancelWindowExitedEvent(
                playerId, moveId, window.TargetCategory));
        }
        _openWindows.Clear();
    }
}
