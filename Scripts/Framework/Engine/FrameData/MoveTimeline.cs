#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.FrameData;

internal sealed class MoveTimeline
{
    private MoveDefinition? _move;
    private int _currentFrame;
    private MovePhase _phase = MovePhase.Idle;

    public MovePhase Phase => _phase;
    public int CurrentFrame => _currentFrame;
    public string? MoveId => _move?.MoveId;
    public MoveDefinition? ActiveMove => _move;
    public int TotalFrames => _move is null ? 0
        : _move.Startup + _move.Active + _move.Recovery;

    public int HitAdvantage => _move?.HitAdvantage ?? 0;
    public int BlockAdvantage => _move?.BlockAdvantage ?? 0;

    public void StartMove(MoveDefinition move)
    {
        ArgumentNullException.ThrowIfNull(move);
        _move = move;
        _currentFrame = 0;
        _phase = MovePhase.Startup;
        ApplyTransitions();
    }

    public void Tick()
    {
        if (_phase == MovePhase.Idle || _move is null) return;

        _currentFrame++;
        ApplyTransitions();
    }

    private void ApplyTransitions()
    {
        if (_move is null) return;

        int startupEnd = _move.Startup;
        int activeEnd = startupEnd + _move.Active;
        int recoveryEnd = activeEnd + _move.Recovery;

        if (_phase == MovePhase.Startup && _currentFrame >= startupEnd)
            _phase = MovePhase.Active;
        else if (_phase == MovePhase.Active && _currentFrame >= activeEnd)
            _phase = MovePhase.Recovery;
        else if (_phase == MovePhase.Recovery && _currentFrame >= recoveryEnd)
        {
            _phase = MovePhase.Idle;
            _move = null;
            _currentFrame = 0;
        }
    }
}
