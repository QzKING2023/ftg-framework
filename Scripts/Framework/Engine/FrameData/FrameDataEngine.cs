#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Godot;

namespace FTG_Framework.Engine.FrameData;

internal sealed class FrameDataEngine : IModule, IFrameDataEngine
{
    private readonly IDataStore _dataStore;
    private readonly MoveTimeline _p1Timeline = new();
    private readonly MoveTimeline _p2Timeline = new();

    public FrameDataEngine(IDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        _dataStore = dataStore;
    }

    public void Initialize(IDataStore dataStore)
    {
        GD.Print("[FrameData] FrameDataEngine initialized.");
    }

    public void Shutdown()
    {
    }

    public void StartMove(int playerId, string moveId)
    {
        var timeline = GetTimeline(playerId);
        if (timeline is null)
        {
            FrameworkLog.Error?.Invoke($"[FrameData] Invalid playerId: {playerId}");
            return;
        }
        if (timeline.Phase != MovePhase.Idle)
            return;
        var move = _dataStore.GetMove(moveId);
        if (move is null)
        {
            FrameworkLog.Error?.Invoke($"[FrameData] Move not found: '{moveId}'");
            return;
        }
        timeline.StartMove(move);
        FrameworkLog.Info?.Invoke($"[FrameData] P{playerId} started '{moveId}' — startup {move.Startup}f, active {move.Active}f, recovery {move.Recovery}f.");
    }

    public void Update()
    {
        UpdatePlayer(1, _p1Timeline);
        UpdatePlayer(2, _p2Timeline);
    }

    public MovePhase GetPhase(int playerId) => GetTimeline(playerId)?.Phase ?? MovePhase.Idle;
    public int GetCurrentFrame(int playerId) => GetTimeline(playerId)?.CurrentFrame ?? 0;
    public string? GetCurrentMoveId(int playerId) => GetTimeline(playerId)?.MoveId;

    private static void UpdatePlayer(int playerId, MoveTimeline timeline)
    {
        if (timeline.Phase == MovePhase.Idle) return;

        EventBus.Instance.Publish(new MoveFrameChangedEvent(
            playerId, timeline.MoveId!, timeline.CurrentFrame, timeline.TotalFrames, timeline.Phase));
        timeline.Tick();
    }

    private MoveTimeline? GetTimeline(int playerId) => playerId switch
    {
        1 => _p1Timeline,
        2 => _p2Timeline,
        _ => null
    };
}
