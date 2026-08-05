#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Engine.Combo;

internal sealed class ComboStateTracker : IModule, IComboStateTracker
{
    private readonly Dictionary<int, ComboTrack> _tracks = new();
    private Action<string, int>? _onComboHit;
    private bool _initialized;

    private sealed class ComboTrack
    {
        public bool Active;
        public int HitCount;
        public string CurrentMoveId = string.Empty;
        public int StartFrame;
        public int PendingAdvantage;
        public bool AttackerIdle;
        public string LastObservedMoveId = string.Empty;
    }

    public ComboStateTracker(IDataStore dataStore)
    {
    }

    public void Initialize(IDataStore dataStore)
    {
        if (_initialized) return;
        _initialized = true;
        EventBus.Instance.Subscribe<FrameAdvancedEvent>(OnFrameAdvanced);
        EventBus.Instance.Subscribe<HitConnectedEvent>(OnHitConnected);
        EventBus.Instance.Subscribe<MoveBlockedEvent>(OnMoveBlocked);
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(OnMoveFrameChanged);
        FrameworkLog.Info?.Invoke("[Combo] ComboStateTracker initialized.");
    }

    public void Shutdown()
    {
        EventBus.Instance.Unsubscribe<FrameAdvancedEvent>(OnFrameAdvanced);
        EventBus.Instance.Unsubscribe<HitConnectedEvent>(OnHitConnected);
        EventBus.Instance.Unsubscribe<MoveBlockedEvent>(OnMoveBlocked);
        EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(OnMoveFrameChanged);
        _tracks.Clear();
        _initialized = false;
    }

    private void OnHitConnected(HitConnectedEvent evt)
    {
        if (!_tracks.TryGetValue(evt.AttackerId, out var track))
        {
            track = new ComboTrack();
            _tracks[evt.AttackerId] = track;
        }

        if (!track.Active)
        {
            track.Active = true;
            track.HitCount = 0;
            track.StartFrame = EventBus.Instance.CurrentFrame;
            EventBus.Instance.Publish(new ComboStartedEvent(evt.AttackerId, evt.MoveId, 1));
        }

        track.HitCount++;
        track.CurrentMoveId = evt.MoveId;
        track.PendingAdvantage = Math.Max(track.PendingAdvantage, evt.HitAdvantage);
        track.AttackerIdle = false;

        _onComboHit?.Invoke(evt.MoveId, track.HitCount);
    }

    private void OnMoveBlocked(MoveBlockedEvent evt)
    {
        if (!_tracks.TryGetValue(evt.AttackerId, out var track))
            return;

        track.AttackerIdle = false;
    }

    private void OnFrameAdvanced(FrameAdvancedEvent evt)
    {
        foreach (var (playerId, track) in _tracks)
        {
            if (!track.Active || !track.AttackerIdle || track.PendingAdvantage <= 0)
                continue;

            track.PendingAdvantage--;
            if (track.PendingAdvantage <= 0)
                EndCombo(playerId, track);
        }
    }

    private void OnMoveFrameChanged(MoveFrameChangedEvent evt)
    {
        if (!_tracks.TryGetValue(evt.PlayerId, out var track) || !track.Active)
            return;

        if (evt.Phase != MovePhase.Idle)
        {
            track.AttackerIdle = false;
            track.LastObservedMoveId = evt.MoveId;
            return;
        }

        if (track.LastObservedMoveId.Length > 0 &&
            evt.MoveId.Length > 0 &&
            track.LastObservedMoveId != evt.MoveId)
            return;

        track.AttackerIdle = true;
        if (track.PendingAdvantage <= 0)
            EndCombo(evt.PlayerId, track);
    }

    private void EndCombo(int playerId, ComboTrack track)
    {
        EventBus.Instance.Publish(new ComboEndedEvent(playerId, track.HitCount, track.CurrentMoveId));
        track.Active = false;
        track.HitCount = 0;
        track.CurrentMoveId = string.Empty;
        track.PendingAdvantage = 0;
        track.AttackerIdle = false;
        track.LastObservedMoveId = string.Empty;
    }

    internal ComboRuntimeSnapshot CaptureComboState()
    {
        var tracks = new Dictionary<int, ComboTrackRuntimeSnapshot>();
        foreach (var (playerId, track) in _tracks)
            tracks[playerId] = new ComboTrackRuntimeSnapshot(
                track.Active, track.HitCount, track.CurrentMoveId, track.StartFrame,
                track.PendingAdvantage, track.AttackerIdle, track.LastObservedMoveId);
        return new ComboRuntimeSnapshot(tracks);
    }

    internal ComboRuntimeSnapshot PrepareComboState(
        ComboRuntimeSnapshot snapshot, SnapshotPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var rebased = new Dictionary<int, ComboTrackRuntimeSnapshot>(snapshot.Tracks.Count);
        foreach (var (playerId, originalTrack) in snapshot.Tracks)
        {
            ComboTrackRuntimeSnapshot track = originalTrack;
            if (playerId is not (1 or 2))
                throw new SnapshotPrepareException(SnapshotParticipantCatalog.Combo,
                    $"Track player '{playerId}' is invalid; only players 1 and 2 exist.");
            if (track.HitCount < 0 || track.PendingAdvantage < 0)
                throw new SnapshotPrepareException(SnapshotParticipantCatalog.Combo,
                    $"P{playerId} track has a negative hit count or pending advantage.");
            if (track.Active)
            {
                if (string.IsNullOrWhiteSpace(track.CurrentMoveId))
                    throw new SnapshotPrepareException(SnapshotParticipantCatalog.Combo,
                        $"P{playerId} active track has no current move identity.");
                // The dispatch counter advances before subscribers observe it, so a
                // track that started in the final dispatched frame records frame + 1.
                // Replay bootstrap/handoff captures run in the live frame domain
                // while the snapshot frame is the replay domain; rebase instead of
                // rejecting so a replay ending mid-combo restores cleanly.
                if (track.StartFrame < 0)
                    throw new SnapshotPrepareException(SnapshotParticipantCatalog.Combo,
                        $"P{playerId} active track start frame {track.StartFrame} is inconsistent with snapshot frame {context.Frame}.");
                if (track.StartFrame > context.Frame + 1 && context.Mode != SnapshotRestoreMode.Normal)
                    track = track with { StartFrame = context.Frame + 1 };
                else if (track.StartFrame > context.Frame + 1)
                    throw new SnapshotPrepareException(SnapshotParticipantCatalog.Combo,
                        $"P{playerId} active track start frame {track.StartFrame} is inconsistent with snapshot frame {context.Frame}.");
                if (track.HitCount < 1)
                    throw new SnapshotPrepareException(SnapshotParticipantCatalog.Combo,
                        $"P{playerId} active track declares hit count {track.HitCount}.");
            }
            rebased[playerId] = track;
        }
        return new ComboRuntimeSnapshot(rebased);
    }

    internal void InstallComboState(ComboRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _tracks.Clear();
        foreach (var (playerId, track) in snapshot.Tracks)
        {
            _tracks[playerId] = new ComboTrack
            {
                Active = track.Active,
                HitCount = track.HitCount,
                CurrentMoveId = track.CurrentMoveId,
                StartFrame = track.StartFrame,
                PendingAdvantage = track.PendingAdvantage,
                AttackerIdle = track.AttackerIdle,
                LastObservedMoveId = track.LastObservedMoveId
            };
        }
    }

    public bool IsActive(int playerId) =>
        _tracks.TryGetValue(playerId, out var track) && track.Active;

    public int GetHitCount(int playerId) =>
        _tracks.TryGetValue(playerId, out var track) ? track.HitCount : 0;

    public string? GetCurrentMoveId(int playerId) =>
        _tracks.TryGetValue(playerId, out var track) ? track.CurrentMoveId : null;

    public int GetComboStartFrame(int playerId) =>
        _tracks.TryGetValue(playerId, out var track) ? track.StartFrame : -1;

    public void SetOnComboHit(Action<string, int>? callback)
    {
        _onComboHit = callback;
    }
}
