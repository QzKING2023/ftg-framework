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

    public void Enter(RuntimePlaybackMode mode, ulong epoch)
    {
        if (mode == RuntimePlaybackMode.None) throw new ArgumentOutOfRangeException(nameof(mode));
        if (ActiveMode != RuntimePlaybackMode.None)
            throw new InvalidOperationException($"[Replay] {ActiveMode} is already active for epoch {ActiveEpoch}.");
        ActiveMode = mode;
        ActiveEpoch = epoch;
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
