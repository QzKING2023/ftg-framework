#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Godot;

namespace FTG_Framework.Engine.FrameData;

internal sealed class FrameDataEngine : IModule, IFrameDataEngine
{
    private readonly IDataStore _dataStore;
    private readonly MoveTimeline _p1Timeline = new();
    private readonly MoveTimeline _p2Timeline = new();
    private readonly CancelWindowTracker _p1CancelTracker = new();
    private readonly CancelWindowTracker _p2CancelTracker = new();
    private readonly List<FrameStateSnapshot> _snapshots = new();
    private readonly List<HitRegistration> _pendingHits = new();
    private int _snapshotCapacity = 600;
    private bool _initialized;

    internal readonly record struct HitRegistration(int AttackerId, int DefenderId, string MoveId, bool IsBlocked);

    internal IReadOnlyList<FrameStateSnapshot> Snapshots => _snapshots.AsReadOnly();

    internal int SnapshotCapacity
    {
        get => _snapshotCapacity;
        set
        {
            if (value < 1)
            {
                FrameworkLog.Error?.Invoke($"[FrameData] SnapshotCapacity must be >= 1, got {value}. Clamped to 1.");
                value = 1;
            }
            _snapshotCapacity = value;
        }
    }

    public int EarliestSnapshotFrame => _snapshots.Count > 0 ? _snapshots[0].Frame : -1;

    public FrameDataEngine(IDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        _dataStore = dataStore;
        _p1Timeline.DataStore = dataStore;
        _p2Timeline.DataStore = dataStore;
    }

    public void Initialize(IDataStore dataStore)
    {
        if (_initialized) return;
        _initialized = true;
        EventBus.Instance.Subscribe<MoveCanceledEvent>(OnMoveCanceled);
        FrameworkLog.Info?.Invoke("[FrameData] FrameDataEngine initialized.");
    }

    public void Shutdown()
    {
        EventBus.Instance.Unsubscribe<MoveCanceledEvent>(OnMoveCanceled);
        _initialized = false;
    }

    public void InterruptAndStart(int playerId, string moveId)
    {
        if (playerId < 1 || playerId > 2)
        {
            FrameworkLog.Error?.Invoke($"[FrameData] Invalid playerId for InterruptAndStart: {playerId}");
            return;
        }

        var move = _dataStore.GetMove(moveId);
        if (move is null)
        {
            FrameworkLog.Error?.Invoke($"[FrameData] InterruptAndStart — move not found: '{moveId}'");
            return;
        }

        var timeline = GetTimeline(playerId);
        if (timeline is null || timeline.Phase == MovePhase.Idle)
        {
            FrameworkLog.Info?.Invoke($"[FrameData] InterruptAndStart — P{playerId} idle, starting normally.");
            StartMove(playerId, moveId);
            return;
        }

        var cancelTracker = GetCancelTracker(playerId);
        if (cancelTracker is null) return;

        cancelTracker.Reset(playerId);
        timeline.StartMove(move);
        cancelTracker.TrackMove(playerId, move);
    }

    private void OnMoveCanceled(MoveCanceledEvent evt)
    {
        InterruptAndStart(evt.PlayerId, evt.ToMove);
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
        GetCancelTracker(playerId)!.TrackMove(playerId, move);
        FrameworkLog.Info?.Invoke($"[FrameData] P{playerId} started '{moveId}' — startup {move.Startup}f, active {move.Active}f, recovery {move.Recovery}f.");
    }

    public void RegisterHit(int attackerId, int defenderId, string moveId, bool isBlocked)
    {
        _pendingHits.Add(new HitRegistration(attackerId, defenderId, moveId, isBlocked));
    }

    public void Update()
    {
        SaveSnapshot();
        UpdatePlayer(1, _p1Timeline, _p1CancelTracker);
        UpdatePlayer(2, _p2Timeline, _p2CancelTracker);
        FlushPendingHits();
    }

    private void SaveSnapshot()
    {
        int frame = EventBus.Instance.CurrentFrame;
        var snapshot = new FrameStateSnapshot(
            frame,
            _p1Timeline.MoveId, _p1Timeline.CurrentFrame, _p1Timeline.Phase,
            _p2Timeline.MoveId, _p2Timeline.CurrentFrame, _p2Timeline.Phase
        );
        _snapshots.Add(snapshot);
        while (_snapshots.Count > _snapshotCapacity)
            _snapshots.RemoveAt(0);
    }

    // Restores the exact observable state of frame `frameNumber`. Snapshot k is
    // the pre-tick state of frame k — the state the panel displayed at frame k.
    // The timeline, however, must resume from snapshot frameNumber+1 (the
    // post-frame state) so the next stepped tick re-executes frame frameNumber+1
    // and replays the original trajectory exactly. The bus counter rewinds to
    // frameNumber+1, keeping snapshots, input history, and displays in one
    // frame domain. Returns false when either snapshot is unavailable (notably
    // when frameNumber is the newest snapshot — there is no frameNumber+1 yet).
    public bool RestoreFrame(int frameNumber)
    {
        int foundIndex = -1;
        for (int i = 0; i + 1 < _snapshots.Count; i++)
        {
            if (_snapshots[i].Frame == frameNumber)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex < 0)
        {
            FrameworkLog.Error?.Invoke($"[FrameDataEngine] No snapshot for frame {frameNumber}.");
            return false;
        }

        var timelineSnapshot = _snapshots[foundIndex + 1];
        _p1Timeline.Restore(timelineSnapshot.P1MoveId, timelineSnapshot.P1CurrentFrame, timelineSnapshot.P1Phase);
        _p2Timeline.Restore(timelineSnapshot.P2MoveId, timelineSnapshot.P2CurrentFrame, timelineSnapshot.P2Phase);

        // Rewinding abandons the old future — drop snapshots past the restore
        // point; snapshot frameNumber+1 is re-saved when that frame re-executes.
        _snapshots.RemoveRange(foundIndex + 1, _snapshots.Count - foundIndex - 1);

        EventBus.Instance.RewindFrameCounter(frameNumber + 1);

        ResyncCancelTracker(1, _p1Timeline, _p1CancelTracker);
        ResyncCancelTracker(2, _p2Timeline, _p2CancelTracker);

        PublishSnapshotState(_snapshots[foundIndex]);
        EventBus.Instance.PublishImmediate(new FrameRewoundEvent(frameNumber));
        return true;
    }

    private static void ResyncCancelTracker(int playerId, MoveTimeline timeline, CancelWindowTracker tracker)
    {
        if (timeline.Phase == MovePhase.Idle || timeline.ActiveMove is null)
        {
            tracker.Reset(playerId, immediate: true);
            return;
        }
        tracker.ResyncFrame(playerId, timeline.ActiveMove, timeline.CurrentFrame);
    }

    public void PublishCurrentState()
    {
        PublishPlayerState(1, _p1Timeline.MoveId, _p1Timeline.CurrentFrame, _p1Timeline.Phase);
        PublishPlayerState(2, _p2Timeline.MoveId, _p2Timeline.CurrentFrame, _p2Timeline.Phase);
    }

    // Panel-facing publish from a snapshot record rather than the live timelines:
    // after a restore the timelines hold the NEXT frame's state, while the panel
    // must show the restored frame's own displayed state.
    private void PublishSnapshotState(FrameStateSnapshot snapshot)
    {
        PublishPlayerState(1, snapshot.P1MoveId, snapshot.P1CurrentFrame, snapshot.P1Phase);
        PublishPlayerState(2, snapshot.P2MoveId, snapshot.P2CurrentFrame, snapshot.P2Phase);
    }

    // One event per player including Idle — panels must not keep rendering an
    // abandoned future's move after a rewind-to-idle.
    private void PublishPlayerState(int playerId, string? moveId, int currentFrame, MovePhase phase)
    {
        if (phase == MovePhase.Idle || moveId is null)
        {
            EventBus.Instance.PublishImmediate(new MoveFrameChangedEvent(
                playerId, string.Empty, 0, 0, MovePhase.Idle));
            return;
        }
        var move = _dataStore.GetMove(moveId);
        int totalFrames = move is null ? 0 : move.Startup + move.Active + move.Recovery;
        EventBus.Instance.PublishImmediate(new MoveFrameChangedEvent(
            playerId, moveId, currentFrame, totalFrames, phase));
    }

    public MovePhase GetPhase(int playerId) => GetTimeline(playerId)?.Phase ?? MovePhase.Idle;
    public int GetCurrentFrame(int playerId) => GetTimeline(playerId)?.CurrentFrame ?? 0;
    public string? GetCurrentMoveId(int playerId) => GetTimeline(playerId)?.MoveId;

    private static void UpdatePlayer(int playerId, MoveTimeline timeline, CancelWindowTracker tracker)
    {
        if (timeline.Phase == MovePhase.Idle) return;

        EventBus.Instance.Publish(new MoveFrameChangedEvent(
            playerId, timeline.MoveId!, timeline.CurrentFrame, timeline.TotalFrames, timeline.Phase));
        tracker.EvaluateFrame(playerId, timeline.MoveId!, timeline.CurrentFrame);

        string? moveIdBeforeTick = timeline.MoveId;
        timeline.Tick();

        if (timeline.Phase == MovePhase.Idle)
            tracker.CloseAll(playerId, moveIdBeforeTick!);
    }

    private void FlushPendingHits()
    {
        foreach (var reg in _pendingHits)
        {
            var move = _dataStore.GetMove(reg.MoveId);
            if (move is null) continue;

            if (reg.IsBlocked)
                EventBus.Instance.Publish(new MoveBlockedEvent(reg.AttackerId, reg.DefenderId, reg.MoveId, move.BlockAdvantage, move.Damage));
            else
                EventBus.Instance.Publish(new HitConnectedEvent(reg.AttackerId, reg.DefenderId, reg.MoveId, move.HitAdvantage, move.Damage));
        }
        _pendingHits.Clear();
    }

    private MoveTimeline? GetTimeline(int playerId) => playerId switch
    {
        1 => _p1Timeline,
        2 => _p2Timeline,
        _ => null
    };

    private CancelWindowTracker? GetCancelTracker(int playerId) => playerId switch
    {
        1 => _p1CancelTracker,
        2 => _p2CancelTracker,
        _ => null
    };
}
