#nullable enable
using System;

namespace FTG_Framework.Core.Replay;

public enum RuntimePlaybackMode : byte
{
    None = 0,
    AuthoritativeReplay = 1,
    TrainingInput = 2
}

/// <summary>Owns the mutually-exclusive runtime playback mode for one lifecycle epoch.</summary>
public sealed class PlaybackModeCoordinator
{
    public RuntimePlaybackMode ActiveMode { get; private set; }
    public ulong ActiveEpoch { get; private set; }
    public int CapturePlayer { get; private set; }
    public ulong CaptureEpoch { get; private set; }

    public void Enter(RuntimePlaybackMode mode, ulong epoch)
    {
        if (mode == RuntimePlaybackMode.None) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == RuntimePlaybackMode.AuthoritativeReplay && CapturePlayer != 0)
            throw new InvalidOperationException($"[Replay] P{CapturePlayer} capture is active for epoch {CaptureEpoch}.");
        if (mode == RuntimePlaybackMode.TrainingInput &&
            CapturePlayer != 0 && CaptureEpoch != epoch)
            throw new InvalidOperationException(
                $"[Replay] Training input epoch {epoch} does not match P{CapturePlayer} capture epoch {CaptureEpoch}.");
        if (ActiveMode != RuntimePlaybackMode.None)
            throw new InvalidOperationException($"[Replay] {ActiveMode} is already active for epoch {ActiveEpoch}.");
        ActiveMode = mode;
        ActiveEpoch = epoch;
    }

    public bool TryEnterCapture(int playerId, ulong epoch, out string error)
    {
        if (playerId is < 1 or > 2 || epoch == 0)
        {
            error = "[Input] Capture requires player 1 or 2 and an active epoch.";
            return false;
        }
        if (CapturePlayer != 0)
        {
            error = $"[Input] P{CapturePlayer} capture is already active for epoch {CaptureEpoch}.";
            return false;
        }
        if (ActiveMode == RuntimePlaybackMode.AuthoritativeReplay)
        {
            error = $"[Input] Authoritative Replay owns epoch {ActiveEpoch}.";
            return false;
        }
        if (ActiveMode == RuntimePlaybackMode.TrainingInput && ActiveEpoch != epoch)
        {
            error = $"[Input] Capture epoch {epoch} does not match training input epoch {ActiveEpoch}.";
            return false;
        }
        CapturePlayer = playerId;
        CaptureEpoch = epoch;
        error = string.Empty;
        return true;
    }

    public void ExitCapture(int playerId)
    {
        if (CapturePlayer == 0) return;
        if (CapturePlayer != playerId)
            throw new InvalidOperationException($"[Input] Cannot release P{playerId}; P{CapturePlayer} owns capture.");
        CapturePlayer = 0;
        CaptureEpoch = 0;
    }

    public void Exit(RuntimePlaybackMode mode)
    {
        if (ActiveMode != mode)
            throw new InvalidOperationException($"[Replay] Cannot exit {mode}; active mode is {ActiveMode}.");
        ActiveMode = RuntimePlaybackMode.None;
        ActiveEpoch = 0;
    }

    internal void RebindEpoch(RuntimePlaybackMode mode, ulong epoch)
    {
        if (ActiveMode != mode)
            throw new InvalidOperationException($"[Replay] Cannot rebind {mode}; active mode is {ActiveMode}.");
        ActiveEpoch = epoch;
    }
}
