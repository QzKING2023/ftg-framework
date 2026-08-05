#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Godot;

namespace FTG_Framework.Input;

internal sealed class ChargeTracker : IModule, IChargeTracker
{
    private readonly IInputHistory _inputHistory;
    private readonly int _retentionFrames;

    private readonly int[,] _chargeStartFrame;
    private readonly int[,] _chargeEndFrame;
    private readonly bool[,] _wasCharging;
    private int _lastUpdateFrame = -1;

    private const int NumChargeDirs = 2;

    public int RetentionFrames => _retentionFrames;

    public ChargeTracker(IInputHistory inputHistory, int retentionFrames = 5)
    {
        ArgumentNullException.ThrowIfNull(inputHistory);
        if (retentionFrames < 0)
            throw new ArgumentException($"[Input] Retention frames must be >= 0, got {retentionFrames}");
        _inputHistory = inputHistory;
        _retentionFrames = retentionFrames;
        _chargeStartFrame = new int[2, NumChargeDirs];
        _chargeEndFrame = new int[2, NumChargeDirs];
        _wasCharging = new bool[2, NumChargeDirs];
        for (int p = 0; p < 2; p++)
            for (int d = 0; d < NumChargeDirs; d++)
            {
                _chargeStartFrame[p, d] = -1;
                _chargeEndFrame[p, d] = -1;
            }
    }

    public void Initialize(IDataStore dataStore)
    {
        GD.Print($"[Input] ChargeTracker initialized — retention: {_retentionFrames}f.");
    }

    public void Shutdown()
    {
    }

    public void Update(int playerId, int currentFrame)
    {
        if (currentFrame < 0)
        {
            GD.PrintErr($"[Input] Invalid currentFrame: {currentFrame}. Must be >= 0.");
            return;
        }
        if (currentFrame < _lastUpdateFrame)
        {
            GD.PrintErr($"[Input] Frame regression detected: {currentFrame} < {_lastUpdateFrame}.");
            return;
        }
        _lastUpdateFrame = currentFrame;

        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return;
        }

        int pIdx = playerId - 1;
        var history = _inputHistory.GetDirectionalHistory(playerId);

        for (int d = 0; d < NumChargeDirs; d++)
        {
            var dir = ChargeDirFromIndex(d);
            bool heldThisFrame = IsDirectionHeldThisFrame(history, dir, currentFrame);

            int start = _chargeStartFrame[pIdx, d];
            int end = _chargeEndFrame[pIdx, d];
            bool wasCharging = _wasCharging[pIdx, d];

            if (heldThisFrame && !wasCharging)
            {
                if (end != -1 && currentFrame - end <= _retentionFrames)
                {
                    _chargeEndFrame[pIdx, d] = -1;
                    _wasCharging[pIdx, d] = true;
                    EventBus.Instance.Publish(new ChargeStateChangedEvent(playerId, (int)dir, true));
                }
                else
                {
                    _chargeStartFrame[pIdx, d] = currentFrame;
                    _chargeEndFrame[pIdx, d] = -1;
                    _wasCharging[pIdx, d] = true;
                    EventBus.Instance.Publish(new ChargeStateChangedEvent(playerId, (int)dir, true));
                }
            }
            else if (heldThisFrame && wasCharging)
            {
                _wasCharging[pIdx, d] = true;
            }
            else if (!heldThisFrame && wasCharging)
            {
                _chargeEndFrame[pIdx, d] = currentFrame - 1;
                _wasCharging[pIdx, d] = false;
                EventBus.Instance.Publish(new ChargeStateChangedEvent(playerId, (int)dir, false));
            }
            else
            {
                if (start != -1 && end != -1 && currentFrame - end > _retentionFrames)
                {
                    _chargeStartFrame[pIdx, d] = -1;
                    _chargeEndFrame[pIdx, d] = -1;
                }
            }
        }
    }

    public bool IsChargeValid(int playerId, DirectionValue chargeDirection, int minDuration, int currentFrame)
    {
        if (minDuration < 0)
            return false;

        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return false;
        }

        int dirIdx = GetChargeDirIndex(chargeDirection);
        if (dirIdx < 0)
            return false;

        int pIdx = playerId - 1;
        int start = _chargeStartFrame[pIdx, dirIdx];
        int end = _chargeEndFrame[pIdx, dirIdx];

        if (start == -1)
            return false;

        int duration = end == -1 ? currentFrame - start + 1 : end - start + 1;
        if (duration < minDuration)
            return false;

        if (end == -1)
            return true;

        return currentFrame - end <= _retentionFrames;
    }

    public int GetChargeDuration(int playerId, DirectionValue chargeDirection, int currentFrame)
    {
        if (playerId < 1 || playerId > 2)
        {
            GD.PrintErr($"[Input] Invalid playerId: {playerId}. Must be 1 or 2.");
            return 0;
        }

        int dirIdx = GetChargeDirIndex(chargeDirection);
        if (dirIdx < 0)
            return 0;

        int pIdx = playerId - 1;
        int start = _chargeStartFrame[pIdx, dirIdx];
        int end = _chargeEndFrame[pIdx, dirIdx];

        if (start == -1)
            return 0;

        return end == -1 ? currentFrame - start + 1 : end - start + 1;
    }

    internal InputRuntimeSnapshot CaptureRuntimeSnapshot(
        System.Collections.Generic.IReadOnlyList<InputEntry> p1Directions,
        System.Collections.Generic.IReadOnlyList<InputEntry> p1Buttons,
        System.Collections.Generic.IReadOnlyList<InputEntry> p2Directions,
        System.Collections.Generic.IReadOnlyList<InputEntry> p2Buttons)
    {
        var starts = new int[4];
        var ends = new int[4];
        var charging = new bool[4];
        for (int player = 0; player < 2; player++)
        for (int direction = 0; direction < NumChargeDirs; direction++)
        {
            int index = player * NumChargeDirs + direction;
            starts[index] = _chargeStartFrame[player, direction];
            ends[index] = _chargeEndFrame[player, direction];
            charging[index] = _wasCharging[player, direction];
        }
        return new InputRuntimeSnapshot(
            System.Linq.Enumerable.ToArray(p1Directions), System.Linq.Enumerable.ToArray(p1Buttons),
            System.Linq.Enumerable.ToArray(p2Directions), System.Linq.Enumerable.ToArray(p2Buttons),
            starts, ends, charging, _lastUpdateFrame);
    }

    internal void InstallRuntimeSnapshot(InputRuntimeSnapshot snapshot)
    {
        for (int player = 0; player < 2; player++)
        for (int direction = 0; direction < NumChargeDirs; direction++)
        {
            int index = player * NumChargeDirs + direction;
            _chargeStartFrame[player, direction] = snapshot.ChargeStarts[index];
            _chargeEndFrame[player, direction] = snapshot.ChargeEnds[index];
            _wasCharging[player, direction] = snapshot.WasCharging[index];
        }
        _lastUpdateFrame = snapshot.LastUpdateFrame;
    }

    internal void ResetPlayer(int playerId)
    {
        if (playerId is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(playerId), "[Input] Invalid player reset.");
        int player = playerId - 1;
        for (int direction = 0; direction < NumChargeDirs; direction++)
        {
            _chargeStartFrame[player, direction] = -1;
            _chargeEndFrame[player, direction] = -1;
            _wasCharging[player, direction] = false;
        }
    }

    private static int GetChargeDirIndex(DirectionValue dir)
    {
        return dir switch
        {
            DirectionValue.Down => 0,
            DirectionValue.Back => 1,
            _ => -1
        };
    }

    private static DirectionValue ChargeDirFromIndex(int index)
    {
        return index switch
        {
            0 => DirectionValue.Down,
            1 => DirectionValue.Back,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Invalid charge direction index")
        };
    }

    private static bool IsDirectionHeldThisFrame(
        System.Collections.Generic.IReadOnlyList<InputEntry> history,
        DirectionValue dir,
        int frame)
    {
        int targetValue = (int)dir;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var entry = history[i];
            if (entry.Frame > frame)
            {
                GD.PrintErr($"[Input] Future-frame entry detected in history: frame {entry.Frame} > current {frame}.");
                continue;
            }
            if (entry.Frame < frame)
                break;
            if (entry.Frame == frame && entry.Value == targetValue)
                return true;
        }
        return false;
    }
}
