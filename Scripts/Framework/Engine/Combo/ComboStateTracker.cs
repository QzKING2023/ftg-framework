#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.Engine.Combo;

internal sealed class ComboStateTracker : IModule, IComboStateTracker
{
    private readonly Dictionary<int, ComboTrack> _tracks = new();
    private readonly HashSet<int> _moveStartedThisFrame = new();
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
        _moveStartedThisFrame.Clear();
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
        _moveStartedThisFrame.Clear();

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
            _moveStartedThisFrame.Add(evt.PlayerId);
            return;
        }

        if (_moveStartedThisFrame.Remove(evt.PlayerId))
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
