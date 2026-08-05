#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using FTG_Framework.Input;

namespace FTG_Framework.Core.Balance;

public enum BalanceTrialState : byte
{
    Idle = 0,
    Prepared = 1,
    Running = 2,
    Completed = 3,
    Invalidated = 4,
    Failed = 5
}

/// <summary>
/// Deterministic balance testbed trial authority (Story 4.1 / FR-34). Binds a
/// training snapshot (AD-20), a Story 2.4 recording, a target player, committed
/// data versions, and an observation window; restores the snapshot and
/// re-simulates the recording from its relative frames through the existing
/// training-input playback path — never converted into an authoritative
/// full-event replay (S4.1-AC01).
///
/// Lifecycle discipline (epic-4 checklist §2.3): the trial subscribes only after
/// its own restore commit, captures the lifecycle epoch at subscription, and
/// re-validates the epoch at every callback. A lifecycle transition (match
/// init, replay start, restore, scene change) invalidates the trial exactly
/// once before any further dispatch (S4.1-AC12). Trial start performs every
/// fallible operation before the first committed swap; the post-swap sequence is
/// fully pre-validated by Prepare (S4.1-AC13).
///
/// Frame convention: the hash trail records one line per completed frame keyed
/// by the FrameAdvanced frame number; move initiations are recorded at their
/// dispatch frame (EventBus.CurrentFrame), one greater than the completed-frame
/// key of the frame in which the move started.
/// </summary>
public sealed class BalanceTrialService
{
    /// <summary>P-4.1: the observation window is the candidate window, or the
    /// terminal move's end frame plus this trailing budget when shorter.</summary>
    private const int TerminalTrailingFrames = 120;

    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = false };

    private readonly TrainingStateService _saveService;
    private readonly TrainingInputService _trainingInput;
    private readonly TrainingInputRecordingLibrary _library;
    private readonly PlaybackModeCoordinator _playbackModes;
    private readonly IDataStore _dataStore;
    private readonly Func<int> _currentFrame;
    private readonly Func<ulong> _currentEpoch;
    private readonly Func<BalanceDataVersions> _versions;
    private readonly Func<int, int, string> _frameHash;
    private readonly Func<int, int> _playerPositionX;
    private readonly Func<int, string?> _playerTerminalMoveId;

    private BalanceTrialBinding? _binding;
    private TrainingInputRecording? _recording;
    private ulong _epochAtSubscription;
    private int _startFrame;
    private int _lastFrame;
    private int _hashedFrames;
    private readonly StringBuilder _hashTrail = new();
    private int _totalDamage;
    private int _maxComboHits;
    private readonly List<BalanceInitiation> _initiations = new();
    private string? _terminalMove;
    private int? _terminalMoveEnd;
    private bool _subscribed;

    public BalanceTrialState State { get; private set; } = BalanceTrialState.Idle;
    public BalanceTrialResult? Result { get; private set; }
    public string? LastError { get; private set; }
    public string? CancellationReason { get; private set; }
    public BalanceTrialBinding? Binding => _binding;

    public BalanceTrialService(
        TrainingStateService saveService,
        TrainingInputService trainingInput,
        TrainingInputRecordingLibrary library,
        PlaybackModeCoordinator playbackModes,
        IDataStore dataStore,
        Func<int>? currentFrame = null,
        Func<ulong>? currentEpoch = null,
        Func<BalanceDataVersions>? versions = null,
        Func<int, int, string>? frameHash = null,
        Func<int, int>? playerPositionX = null,
        Func<int, string?>? playerTerminalMoveId = null)
    {
        ArgumentNullException.ThrowIfNull(saveService);
        ArgumentNullException.ThrowIfNull(trainingInput);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(playbackModes);
        ArgumentNullException.ThrowIfNull(dataStore);
        _saveService = saveService;
        _trainingInput = trainingInput;
        _library = library;
        _playbackModes = playbackModes;
        _dataStore = dataStore;
        _currentFrame = currentFrame ?? (() => EventBus.Instance.CurrentFrame);
        _currentEpoch = currentEpoch ?? (() => EventBus.Instance.LifecycleEpoch);
        _versions = versions ?? (() => new BalanceDataVersions(0, 0));
        // A missing hash provider must not silently produce a meaningless trail:
        // a bare frame-number trail would compare "deterministically" while
        // capturing nothing (AC06).
        _frameHash = frameHash ?? (static (_, _) =>
            throw new InvalidOperationException("[Trial] No frame hash provider was supplied."));
        _playerPositionX = playerPositionX ?? (_ => 0);
        _playerTerminalMoveId = playerTerminalMoveId ?? (_ => null);
    }

    /// <summary>Validates the complete candidate before any restore or input
    /// injection; rejection leaves the current training state and prior
    /// comparison results unchanged (S4.1-AC07). Exactly-once: preparing while a
    /// trial is Prepared or Running is rejected.</summary>
    public bool TryPrepare(BalanceTrialCandidate candidate, out string error)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (State is BalanceTrialState.Prepared or BalanceTrialState.Running)
        {
            error = $"[Trial] A trial is already {State.ToString().ToLowerInvariant()}; complete, cancel, or invalidate it before preparing another.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(candidate.SnapshotPath) || !File.Exists(candidate.SnapshotPath))
        {
            error = "[Trial] Snapshot path is required and must exist.";
            return false;
        }
        if (candidate.TargetPlayer is not (1 or 2))
        {
            error = "[Trial] Target player must be 1 or 2.";
            return false;
        }
        if (candidate.ObservationWindowFrames < 1)
        {
            error = "[Trial] Observation window must be at least one frame.";
            return false;
        }
        if (!TrainingInputRecording.IsValidName(candidate.RecordingName))
        {
            error = "[Trial] Recording name is required and must be a valid recording name.";
            return false;
        }

        if (!_saveService.TryInspect(candidate.SnapshotPath,
                out TrainingStateService.TrainingSaveSlotInfo? info, out string inspectError))
        {
            error = $"[Trial] Snapshot is invalid: {inspectError}";
            return false;
        }
        // TryInspect success guarantees the slot info (out contract); the
        // null-forgiving documents that invariant for nullable analysis.
        if (candidate.ExpectedSnapshotSha256 is not null &&
            !string.Equals(candidate.ExpectedSnapshotSha256, info!.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            error = $"[Trial] Snapshot identity mismatch: expected {candidate.ExpectedSnapshotSha256}, actual {info.Sha256}.";
            return false;
        }

        StateSnapshot snapshot;
        try
        {
            snapshot = TrainingStatePersistence.Load(candidate.SnapshotPath, out _);
        }
        catch (Exception ex) when (ex is TrainingStateLoadException or IOException or FormatException)
        {
            // TryInspect succeeded moments ago; a failure here is a TOCTOU race
            // (file truncated/removed between the two reads) — reject cleanly
            // instead of throwing out of the out-error contract (AC07).
            error = $"[Trial] Snapshot could not be loaded: {ex.Message}";
            return false;
        }
        string? dataRefError = ValidateSnapshotDataReferences(snapshot);
        if (dataRefError is not null)
        {
            error = dataRefError;
            return false;
        }
        if (DecodeComponent<TrainingInputRuntimeSnapshot>(snapshot,
                SnapshotParticipantCatalog.TrainingInput, out string? trainingError) is not { } training)
        {
            error = trainingError ?? "[Trial] Training-input component could not be decoded.";
            return false;
        }
        if (training.Session is not null)
        {
            error = "[Trial] Snapshot contains an active training playback session; trial snapshots must be session-free.";
            return false;
        }
        TrainingInputRecordingRuntimeSnapshot? entry = training.Library.FirstOrDefault(item =>
            string.Equals(item.Name, candidate.RecordingName, StringComparison.Ordinal));
        if (entry is null)
        {
            error = $"[Trial] Recording '{candidate.RecordingName}' does not exist in the snapshot's training-input library.";
            return false;
        }
        TrainingInputRecording decoded;
        try
        {
            decoded = TrainingInputRecordingCodec.Decode(Convert.FromBase64String(entry.CodecPayloadBase64));
        }
        catch (Exception ex) when (ex is TrainingInputRecordingException or FormatException
            or ArgumentException or OverflowException)
        {
            error = $"[Trial] Recording '{candidate.RecordingName}' failed to decode: {ex.Message}";
            return false;
        }

        if (_playbackModes.ActiveMode == RuntimePlaybackMode.AuthoritativeReplay)
        {
            error = "[Trial] Authoritative replay is active; training input playback and authoritative replay cannot share an epoch.";
            return false;
        }
        if (_playbackModes.ActiveMode == RuntimePlaybackMode.TrainingInput || _playbackModes.CapturePlayer != 0)
        {
            error = $"[Trial] Training playback or capture already owns the session (mode={_playbackModes.ActiveMode}, capture player={_playbackModes.CapturePlayer}).";
            return false;
        }

        TrainingStateService.TrainingSaveSlotInfo inspected = info!;
        _binding = new BalanceTrialBinding(
            candidate.SnapshotPath, inspected.Sha256, candidate.RecordingName, candidate.TargetPlayer,
            _versions(), candidate.ObservationWindowFrames, candidate.ReferenceDatasetId);
        _recording = decoded;
        _hashTrail.Clear();
        _initiations.Clear();
        _totalDamage = 0;
        _maxComboHits = 0;
        _hashedFrames = 0;
        _terminalMove = null;
        _terminalMoveEnd = null;
        LastError = null;
        CancellationReason = null;
        Result = null;
        State = BalanceTrialState.Prepared;
        error = string.Empty;
        return true;
    }

    /// <summary>Starts the prepared trial: restores the snapshot (the committed
    /// swap) and then performs the fully pre-validated post-swap sequence
    /// (release restored session, start playback, subscribe, capture epoch).
    /// A failure before the swap aborts cleanly with no mutation (S4.1-AC13);
    /// a post-swap failure is an invariant violation that marks the trial Failed
    /// and releases ownership without touching live state.</summary>
    public bool TryStart(out string error)
    {
        if (State != BalanceTrialState.Prepared || _binding is null || _recording is null)
        {
            error = State == BalanceTrialState.Running
                ? "[Trial] Trial is already running; trial start is exactly-once."
                : "[Trial] No prepared trial to start.";
            return false;
        }

        if (!_saveService.TryRestore(_binding.SnapshotPath, out string restoreError))
        {
            State = BalanceTrialState.Idle;
            error = $"[Trial] Restore failed before trial start; no live state was mutated: {restoreError}";
            return false;
        }

        // Re-capture the committed versions at start: a tuning commit between
        // Prepare and Start must be reflected in the binding the trial runs under
        // (AC01); the pre-restore validation already ran against live data.
        BalanceTrialBinding binding = _binding with { Versions = _versions() };
        _binding = binding;
        TrainingInputRecording recording = _recording;
        try
        {
            // No unconditional StopPlayback here: a playback owned by something
            // else (started between Prepare and Start) must be rejected by
            // TryStartPlayback's ownership rules, not killed (AC07).
            if (!_trainingInput.TryStartPlayback(
                    recording,
                    binding.TargetPlayer,
                    _currentFrame(),
                    loop: false,
                    _currentEpoch(),
                    out string playbackError))
            {
                throw new InvalidOperationException(playbackError);
            }
            Subscribe();
            _epochAtSubscription = _currentEpoch();
            _startFrame = _currentFrame();
            State = BalanceTrialState.Running;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _trainingInput.StopPlayback();
            Unsubscribe();
            LastError = $"[Trial] Post-restore trial start invariant violated: {ex.Message}";
            State = BalanceTrialState.Failed;
            error = LastError;
            return false;
        }
    }

    /// <summary>Host frame hook: the host drives the frame pipeline (playback
    /// injection, input resolution, engine updates, ProcessFrame) while the
    /// trial observes the completed frames through its EventBus subscriptions.
    /// No call is required from the host for observation; this method exists for
    /// symmetric teardown and is intentionally inert.</summary>
    public void ProcessFrame() { }

    public void Cancel(string? reason = null) => Invalidate(reason ?? "[Trial] Cancel requested.");

    public void Shutdown()
    {
        _trainingInput.StopPlayback();
        Unsubscribe();
        State = BalanceTrialState.Idle;
        Result = null;
        _binding = null;
        _recording = null;
    }

    // ─── Observation ────────────────────────────────────────────────

    private void OnFrameAdvanced(FrameAdvancedEvent evt)
    {
        if (State != BalanceTrialState.Running) return;
        if (!ValidateEpoch()) return;
        _hashTrail.Append(_frameHash(evt.FrameNumber, _binding!.TargetPlayer)).Append('\n');
        _lastFrame = evt.FrameNumber;
        _hashedFrames++;
        // P-4.1: 600 frames after first injection, or the terminal move's end
        // frame plus 120 when shorter.
        if (_hashedFrames >= _binding!.ObservationWindowFrames ||
            (_terminalMoveEnd is { } terminalEnd && evt.FrameNumber >= terminalEnd + TerminalTrailingFrames))
            CompleteTrial();
    }

    private void OnHitConnected(HitConnectedEvent evt)
    {
        if (State != BalanceTrialState.Running) return;
        if (!ValidateEpoch()) return;
        if (evt.AttackerId != _binding!.TargetPlayer) return;
        try { _totalDamage = checked(_totalDamage + evt.Damage); }
        catch (OverflowException) { /* P-COMBO overflow rule: preserve the prior total. */ }
    }

    private void OnMoveStarted(MoveStartedEvent evt)
    {
        if (State != BalanceTrialState.Running) return;
        if (!ValidateEpoch()) return;
        if (evt.PlayerId != _binding!.TargetPlayer) return;
        RecordInitiation(evt.MoveId);
    }

    // A cancel starts the next move without a MoveStartedEvent; it is an
    // initiation boundary too (AC09 version attribution) and the current
    // move for the terminal-state metric (AC05).
    private void OnMoveCanceled(MoveCanceledEvent evt)
    {
        if (State != BalanceTrialState.Running) return;
        if (!ValidateEpoch()) return;
        if (evt.PlayerId != _binding!.TargetPlayer) return;
        RecordInitiation(evt.ToMove);
    }

    private void RecordInitiation(string moveId)
    {
        int frame = EventBus.Instance.CurrentFrame;
        _initiations.Add(new BalanceInitiation(frame, moveId, _versions()));
        _terminalMove = moveId;
        var move = _dataStore.GetMove(moveId);
        _terminalMoveEnd = move is null
            ? null
            : frame + move.Startup + move.Active + move.Recovery;
    }

    private void OnComboEnded(ComboEndedEvent evt)
    {
        if (State != BalanceTrialState.Running) return;
        if (!ValidateEpoch()) return;
        if (evt.PlayerId != _binding!.TargetPlayer) return;
        if (evt.TotalHits > _maxComboHits) _maxComboHits = evt.TotalHits;
    }

    private bool ValidateEpoch()
    {
        if (_currentEpoch() == _epochAtSubscription) return true;
        Invalidate("[Trial] Lifecycle epoch changed during the trial.");
        return false;
    }

    private void CompleteTrial()
    {
        if (State != BalanceTrialState.Running || _binding is null) return;
        // Terminal move is the last move actually executed (started or
        // cancel-initiated); the live query is a fallback only when no move was
        // observed during the window.
        string? terminalMove = _terminalMove ?? _playerTerminalMoveId(_binding.TargetPlayer);
        Result = new BalanceTrialResult(
            _binding, _startFrame, _lastFrame,
            _hashTrail.ToString(), _totalDamage, _maxComboHits,
            terminalMove, _playerPositionX(_binding.TargetPlayer),
            _initiations.AsReadOnly());
        State = BalanceTrialState.Completed;
        Unsubscribe();
        _trainingInput.StopPlayback();
    }

    // ─── Lifecycle discipline (AC12) ─────────────────────────────────

    private void Subscribe()
    {
        if (_subscribed) return;
        EventBus.Instance.Subscribe<FrameAdvancedEvent>(OnFrameAdvanced);
        EventBus.Instance.Subscribe<HitConnectedEvent>(OnHitConnected);
        EventBus.Instance.Subscribe<MoveStartedEvent>(OnMoveStarted);
        EventBus.Instance.Subscribe<MoveCanceledEvent>(OnMoveCanceled);
        EventBus.Instance.Subscribe<ComboEndedEvent>(OnComboEnded);
        EventBus.Instance.Subscribe<MatchInitializedEvent>(OnMatchInitialized);
        EventBus.Instance.Subscribe<ReplayStartedEvent>(OnReplayStarted);
        EventBus.Instance.Subscribe<StateRestoredEvent>(OnStateRestored);
        EventBus.Instance.Subscribe<SceneChangingEvent>(OnSceneChanging);
        EventBus.Instance.Subscribe<FrameRewoundEvent>(OnFrameRewound);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        EventBus.Instance.Unsubscribe<FrameAdvancedEvent>(OnFrameAdvanced);
        EventBus.Instance.Unsubscribe<HitConnectedEvent>(OnHitConnected);
        EventBus.Instance.Unsubscribe<MoveStartedEvent>(OnMoveStarted);
        EventBus.Instance.Unsubscribe<MoveCanceledEvent>(OnMoveCanceled);
        EventBus.Instance.Unsubscribe<ComboEndedEvent>(OnComboEnded);
        EventBus.Instance.Unsubscribe<MatchInitializedEvent>(OnMatchInitialized);
        EventBus.Instance.Unsubscribe<ReplayStartedEvent>(OnReplayStarted);
        EventBus.Instance.Unsubscribe<StateRestoredEvent>(OnStateRestored);
        EventBus.Instance.Unsubscribe<SceneChangingEvent>(OnSceneChanging);
        EventBus.Instance.Unsubscribe<FrameRewoundEvent>(OnFrameRewound);
        _subscribed = false;
    }

    private void OnMatchInitialized(MatchInitializedEvent _) => Invalidate("[Trial] Match initialized.");
    private void OnReplayStarted(ReplayStartedEvent _) => Invalidate("[Trial] Replay started.");
    private void OnStateRestored(StateRestoredEvent _) => Invalidate("[Trial] State restored.");
    private void OnSceneChanging(SceneChangingEvent _) => Invalidate("[Trial] Scene changing.");
    private void OnFrameRewound(FrameRewoundEvent _) => Invalidate("[Trial] Frame rewound.");

    /// <summary>Invalidates the trial exactly once, before any further dispatch:
    /// ownership (playback) is released, subscriptions are removed, and no
    /// metric is recorded from a stale-epoch event (S4.1-AC12).</summary>
    private void Invalidate(string reason)
    {
        if (State is not (BalanceTrialState.Prepared or BalanceTrialState.Running)) return;
        // Checklist §2.3: invalidate subscriptions before releasing trial-owned state.
        Unsubscribe();
        _trainingInput.StopPlayback();
        CancellationReason = reason;
        State = BalanceTrialState.Invalidated;
        _hashTrail.Clear();
        _initiations.Clear();
        _totalDamage = 0;
        _maxComboHits = 0;
        Result = null;
    }

    // ─── Pre-restore snapshot validation ────────────────────────────

    private string? ValidateSnapshotDataReferences(StateSnapshot snapshot)
    {
        if (DecodeComponent<FrameDataRuntimeSnapshot>(snapshot,
                SnapshotParticipantCatalog.FrameData, out string? frameError) is not { } frameData)
            return frameError;
        foreach ((int player, string? moveId, MovePhase phase) in new[]
                 {
                     (1, frameData.State.P1MoveId, frameData.State.P1Phase),
                     (2, frameData.State.P2MoveId, frameData.State.P2Phase)
                 })
        {
            // A stale move id is a dangling reference regardless of phase: it
            // still flows into every frame hash line via the restored state.
            if ((!string.IsNullOrWhiteSpace(moveId) && _dataStore.GetMove(moveId) is null) ||
                (phase != MovePhase.Idle && string.IsNullOrWhiteSpace(moveId)))
                return $"[Trial] Snapshot P{player} move '{moveId}' has no registered definition in the current dataset (dangling reference).";
        }
        if (snapshot.Components.Any(component => component.Discriminator == SnapshotParticipantCatalog.Combo))
        {
            if (DecodeComponent<ComboRuntimeSnapshot>(snapshot,
                    SnapshotParticipantCatalog.Combo, out string? comboError) is not { } combo)
                return comboError ?? "[Trial] Snapshot combo component could not be decoded.";
            foreach (var (playerId, track) in combo.Tracks)
                if (track.Active && !string.IsNullOrWhiteSpace(track.CurrentMoveId) &&
                    _dataStore.GetMove(track.CurrentMoveId) is null)
                    return $"[Trial] Snapshot P{playerId} combo move '{track.CurrentMoveId}' has no registered definition (dangling reference).";
        }
        return null;
    }

    private static TState? DecodeComponent<TState>(
        StateSnapshot snapshot, string discriminator, out string? error) where TState : class
    {
        SnapshotComponent? component = snapshot.Components.FirstOrDefault(
            candidate => candidate.Discriminator == discriminator);
        if (component is null)
        {
            error = $"[Trial] Snapshot is missing required component '{discriminator}'.";
            return null;
        }
        try
        {
            TState? decoded = JsonSerializer.Deserialize<TState>(component.Payload, PayloadOptions);
            if (decoded is null)
            {
                error = $"[Trial] Snapshot component '{discriminator}' decoded to null.";
                return null;
            }
            error = null;
            return decoded;
        }
        catch (JsonException ex)
        {
            error = $"[Trial] Snapshot component '{discriminator}' is not a valid value snapshot: {ex.Message}";
            return null;
        }
    }
}
