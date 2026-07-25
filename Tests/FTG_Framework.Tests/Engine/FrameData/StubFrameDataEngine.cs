#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Tests;

internal sealed class StubFrameDataEngine : IFrameDataEngine
{
    private readonly Dictionary<int, int> _frames = new();

    public void SetCurrentFrame(int playerId, int frame) => _frames[playerId] = frame;

    public int EarliestSnapshotFrame => -1;

    public int GetCurrentFrame(int playerId) =>
        _frames.TryGetValue(playerId, out var frame) ? frame : 0;

    public MovePhase GetPhase(int playerId) => MovePhase.Idle;

    public string? GetCurrentMoveId(int playerId) => null;

    public void StartMove(int playerId, string moveId) { }

    public void InterruptAndStart(int playerId, string moveId) { }

    public void RegisterHit(int attackerId, int defenderId, string moveId, bool isBlocked) { }

    public void Update() { }

    public bool RestoreFrame(int frameNumber) => false;
}
