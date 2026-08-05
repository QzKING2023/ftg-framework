#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FTG_Framework.Core.Replay;

namespace FTG_Framework.Core;

/// <summary>
/// Process-global, single-threaded event bus with an 8-phase frame pipeline (AD-12).
///
/// <b>Dispatch Semantics</b>
///
/// <b>1. LIFO same-type dispatch.</b> Within a single dispatch phase, events of the
/// same type are dispatched in last-in-first-out order — the most recently published
/// event fires first. This matters when the same subscriber publishes multiple events
/// of the same type during one frame (e.g., all CancelWindowEntered fire before any
/// CancelWindowExited within the same phase group).
///
/// <b>2. Per-phase grouping.</b> <c>ProcessFrame()</c> partitions dispatch into eight
/// ordered phases. All events of a given type are dispatched together before moving
/// to the next type. Types within a phase are dispatched in the order listed in
/// <c>ProcessFrame()</c>.
///
/// <b>3. Same-frame response queuing.</b> If a subscriber publishes an event while
/// a dispatch is already in progress, the new event is queued in <c>_nextQueue</c>
/// and dispatched during the following frame. This prevents unbounded re-entry and
/// keeps frame boundaries well-defined.
///
/// <para><b>Frame-number semantics.</b> <c>CurrentFrame</c> is an Int32 counter
/// advanced exactly once per <c>ProcessFrame()</c>. Reaching <c>Int32.MaxValue</c>
/// is a contract violation: <c>ProcessFrame()</c> throws rather than wrap, because
/// a silent wrap would corrupt deterministic hashing and replay frame catalogs.</para>
///
/// <b>Eight-phase pipeline (AD-12):</b>
/// <list type="number">
/// <item>Phase 0 — Hot-Reload: drain <c>DataReloadedEvent</c> from FileWatcher</item>
/// <item>Phase 1 — Frame tick: <c>FrameAdvancedEvent</c> (auto-injected)</item>
/// <item>Phase 2 — Input System: <c>InputReceivedEvent</c>, <c>InputBufferExpiredEvent</c>, <c>ChargeStateChangedEvent</c></item>
/// <item>Phase 3 — Frame Data Engine: <c>MoveStartedEvent</c>, <c>MoveFrameChangedEvent</c>, <c>CancelWindowEnteredEvent</c>, <c>CancelWindowExitedEvent</c></item>
/// <item>Phase 4 — Physics: <c>HitConnectedEvent</c>, <c>MoveBlockedEvent</c>, <c>KnockbackAppliedEvent</c></item>
/// <item>Phase 5 — State Machine: <c>StateChangedEvent</c>, <c>StateStackChangedEvent</c></item>
/// <item>Phase 6 — Combo System: <c>ComboStartedEvent</c>, <c>MoveCanceledEvent</c>, <c>ComboEndedEvent</c></item>
/// <item>Phase 7 — UI: <c>CharacterSelectedEvent</c>, <c>MatchInitializedEvent</c>, <c>SceneChangingEvent</c>, <c>SceneChangedEvent</c>, <c>ReplayStartedEvent</c>, <c>ReplayEndedEvent</c>, <c>ReplayPausedEvent</c></item>
/// </list>
/// </summary>
public sealed class EventBus
{
    private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
    
    public static EventBus Instance => _instance.Value;

    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly record struct Envelope(object Payload, ulong Epoch, int Frame, long Sequence = 0);
    private readonly List<Envelope> _currentQueue = new();
    private readonly List<Envelope> _nextQueue = new();
    private readonly List<Envelope> _quarantinedQueue = new();
    private readonly ConcurrentQueue<Envelope> _pendingReloads = new();
    private readonly object _epochSync = new();
    private long _testGeneration;
    private int? _testOwnerThreadId;
    private bool _dispatching;
    private int _frameNumber;
    private ulong _lifecycleEpoch = 1;
    private ulong? _dispatchEpoch;
    private ulong? _reservedEpoch;
    private bool _snapshotQuiesced;
    private bool _publicationProhibited;
    private bool _replayApplyActive;
    private long _nextEnvelopeSequence;

    public int CurrentFrame => _frameNumber;
    internal ulong LifecycleEpoch => _lifecycleEpoch;
    internal ulong DispatchEpoch => _dispatchEpoch ?? throw new InvalidOperationException("[EventBus] Dispatch context is only valid during a subscriber callback.");

    public bool Paused { get; set; }
    public bool StepRequested { get; set; }

    /// <summary>
    /// Attach a recorder to capture all dispatched events for replay.
    /// Set to null (default) when not recording — zero overhead.
    /// Must be null during replay playback to prevent double-recording of injected events.
    /// </summary>
    internal IReplayRecorder? Recorder { get; set; }

    /// <summary>
    /// When true, ProcessFrame skips the auto-generated FrameAdvancedEvent in Phase 1.
    /// Set during replay playback to prevent double-dispatch of FrameAdvancedEvent
    /// (once from the injected recording, once from ProcessFrame).
    /// </summary>
    internal bool SuppressFrameAdvanced { get; set; }

    private int _dispatchFrame;

    // Rewinds after a state restore: the next processed frame re-executes as
    // nextFrame, keeping snapshots, input history, and displays in one frame
    // domain. Queued events from the abandoned future are purged.
    internal void RewindFrameCounter(int nextFrame)
    {
        _frameNumber = nextFrame;
        _currentQueue.Clear();
        _nextQueue.Clear();
        while (_pendingReloads.TryDequeue(out _)) { }
    }

    // Thread-safe enqueue for FileWatcher running on OS background threads.
    // Drained at step 0 of ProcessFrame on the main thread.
    internal void EnqueueDataReload(Events.DataReloadedEvent evt)
    {
        lock (_epochSync)
            _pendingReloads.Enqueue(new Envelope(evt, _lifecycleEpoch, _frameNumber, NextEnvelopeSequence()));
    }

    private EventBus() { }

    public void Subscribe<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
            _subscribers[type] = new List<Delegate>();
        if (!_subscribers[type].Contains(handler))
            _subscribers[type].Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var handlers))
        {
            handlers.Remove(handler);
            if (handlers.Count == 0)
                _subscribers.Remove(type);
        }
    }

    // Events published outside a dispatch go to _currentQueue (processed this frame).
    // Events published DURING a dispatch (re-entrant publish) go to _nextQueue and
    // are held until the next frame — this is the same-frame response queuing rule.
    public void Publish<T>(T evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));

        lock (_epochSync)
        {
            if (_publicationProhibited)
                throw new InvalidOperationException("[EventBus] Publication is prohibited from a StateRestored observer.");
            Type runtimeType = evt.GetType();
            // Session-level replay apply: derived publications are suppressed so
            // injected events execute exactly once (owner-applier + queue dispatch),
            // never re-broadcast by cancel-chain or frame-update subscribers.
            if (_replayApplyActive && EventTypeRegistry.IsRegistered(runtimeType))
                return;
            if (IsLifecycle(evt) && _lifecycleEpoch == ulong.MaxValue)
                throw new InvalidOperationException("[EventBus] Lifecycle epoch exhausted.");
            Enqueue(evt!, runtimeType);
        }
    }

    private void Enqueue<T>(T evt, Type runtimeType)
    {
        if (_snapshotQuiesced)
            _quarantinedQueue.Add(new Envelope(evt!, _lifecycleEpoch, _frameNumber, NextEnvelopeSequence()));
        else if (_dispatching)
            _nextQueue.Add(new Envelope(evt!, _lifecycleEpoch, _frameNumber, NextEnvelopeSequence()));
        else
            _currentQueue.Add(new Envelope(evt!, _lifecycleEpoch, _frameNumber, NextEnvelopeSequence()));
    }

    /// <summary>
    /// Replay injection seam: queues a recorded event for the current frame's
    /// dispatch pipeline, bypassing session-level replay suppression so the
    /// recorded envelope still executes. Effects land at the recorded position
    /// (end of frame k, visible in the hash at k+1) matching the recording side.
    /// </summary>
    internal void InjectReplayEvent(object evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        lock (_epochSync)
        {
            if (_publicationProhibited)
                throw new InvalidOperationException("[EventBus] Publication is prohibited from a StateRestored observer.");
            if (IsLifecycle(evt) && _lifecycleEpoch == ulong.MaxValue)
                throw new InvalidOperationException("[EventBus] Lifecycle epoch exhausted.");
            Enqueue(evt, evt.GetType());
        }
    }

    // Bypasses the frame queue and dispatches to subscribers synchronously. Required
    // for rewind/restore events: while paused, ProcessFrame never runs, so queued
    // events would either never arrive or arrive stale (LIFO) on resume.
    public void PublishImmediate<T>(T evt) where T : struct
    {
        if (_publicationProhibited)
            throw new InvalidOperationException("[EventBus] Publication is prohibited from a StateRestored observer.");
        if (_replayApplyActive && EventTypeRegistry.IsRegistered(typeof(T)))
            return;
        var envelope = new Envelope(evt, _lifecycleEpoch, _frameNumber, NextEnvelopeSequence());
        if (IsLifecycle(evt))
            envelope = ActivateLifecycle(envelope);
        DispatchEnvelope(envelope, evt);
    }

    private void DispatchEnvelope<T>(Envelope envelope, T evt) where T : struct
    {
        if (envelope.Epoch != _lifecycleEpoch)
            return;
        // Record rewind/restore events that bypass the queue.
        // IMPORTANT: During replay playback, Recorder must be null to prevent
        // double-recording of injected events. See ReplayOrchestrator.
        Recorder?.Record(_dispatchFrame, evt);

        if (_subscribers.TryGetValue(typeof(T), out var handlers))
        {
            var snapshot = handlers.ToArray();
            ulong? previousDispatchEpoch = _dispatchEpoch;
            _dispatchEpoch = envelope.Epoch;
            try
            {
                foreach (var handler in snapshot)
                {
                    if (envelope.Epoch != _lifecycleEpoch)
                        break;
                    try
                    {
                        ((Action<T>)handler)(evt);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[EventBus] Handler for {typeof(T).Name} threw: {ex}");
                    }
                }
            }
            finally { _dispatchEpoch = previousDispatchEpoch; }
        }
    }

    // 8-phase dispatch pipeline (AD-12). Each phase dispatches all queued events
    // of its declared types before the next phase begins. The ordering is fixed:
    //   0. Hot-Reload      (DataReloaded — drained from FileWatcher)
    //   1. Frame tick      (FrameAdvanced — auto-injected)
    //   2. Input System    (InputReceived, InputBufferExpired, ChargeStateChanged)
    //   3. Frame Data Engine (MoveStarted, MoveFrameChanged, CancelWindow*)
    //   4. Physics         (HitConnected, MoveBlocked, KnockbackApplied)
    //   5. State Machine   (StateChanged, StateStackChanged)
    //   6. Combo System    (ComboStarted, MoveCanceled, ComboEnded)
    //   7. UI              (CharacterSelected, MatchInitialized, Scene*, Replay*)
    // This ordering guarantees that downstream systems see the upstream
    // system's events before their own subscribers run.
    public void ProcessFrame()
    {
        if (_frameNumber == int.MaxValue)
            throw new InvalidOperationException(
                "[EventBus] Frame counter exhausted; Int32 frame numbers cannot advance beyond Int32.MaxValue.");
        _dispatching = true;
        _dispatchFrame = _frameNumber;
        try
        {
            // Phase 0: Hot-Reload — drain DataReloadedEvent from FileWatcher
            while (_pendingReloads.TryDequeue(out var reloadEnvelope))
                _currentQueue.Add(reloadEnvelope);
            CoalesceDataReloads();
            DispatchType<Events.DataReloadedEvent>();

            // Phase 1: Frame tick
            if (!SuppressFrameAdvanced)
                _currentQueue.Add(new Envelope(new Events.FrameAdvancedEvent(_frameNumber), _lifecycleEpoch, _frameNumber, NextEnvelopeSequence()));
            _frameNumber++;
            DispatchType<Events.FrameAdvancedEvent>();

            // Phase 2: Input System events
            DispatchType<Events.InputReceivedEvent>();
            DispatchType<Events.InputBufferExpiredEvent>();
            DispatchType<Events.ChargeStateChangedEvent>();

            // Phase 3: Frame Data Engine events
            DispatchType<Events.MoveStartedEvent>();
            DispatchType<Events.MoveFrameChangedEvent>();
            DispatchType<Events.CancelWindowEnteredEvent>();
            DispatchType<Events.CancelWindowExitedEvent>();

            // Phase 4: Physics events
            DispatchType<Events.HitConnectedEvent>();
            DispatchType<Events.MoveBlockedEvent>();
            DispatchType<Events.KnockbackAppliedEvent>();

            // Phase 5: State Machine events
            DispatchType<Events.StateChangedEvent>();
            DispatchType<Events.StateStackChangedEvent>();

            // Phase 6: Combo System events
            DispatchType<Events.ComboStartedEvent>();
            DispatchType<Events.MoveCanceledEvent>();
            DispatchType<Events.ComboEndedEvent>();

            // Phase 7: UI — read-only observer
            DispatchType<Events.CharacterSelectedEvent>();
            DispatchType<Events.MatchInitializedEvent>();
            DispatchType<Events.SceneChangingEvent>();
            DispatchType<Events.SceneChangedEvent>();
            DispatchType<Events.ReplayStartedEvent>();
            DispatchType<Events.ReplayEndedEvent>();
            DispatchType<Events.ReplayPausedEvent>();
            DispatchType<Events.StateRestoredEvent>();

            System.Diagnostics.Debug.Assert(_currentQueue.Count == 0,
                $"[EventBus] {_currentQueue.Count} unhandled event(s) remain after dispatch — unknown event type in queue.");
        }
        finally
        {
            _dispatching = false;

            // Swap queues: next frame's events become current
            _currentQueue.Clear();
            if (_nextQueue.Count > 0)
            {
                _currentQueue.AddRange(_nextQueue);
                _nextQueue.Clear();
            }
        }
    }

    // Returns the number of registered handlers for the given event type, or 0
    // if no handlers are registered. Read-only diagnostic query — no side effects.
    public int GetSubscriberCount<T>() where T : struct
    {
        return _subscribers.TryGetValue(typeof(T), out var handlers) ? handlers.Count : 0;
    }

    // Returns all event types known to the bus: types that have had subscribers
    // registered, plus the statically-known dispatch-pipeline types. Used by
    // diagnostic tooling to discover subscribable event types.
    // NOTE: when a new event type is added to ProcessFrame, add it here too.
    public IReadOnlyList<Type> GetKnownEventTypes()
    {
        var types = new List<Type>(_subscribers.Keys);
        void AddIfMissing<T>() where T : struct
        {
            var t = typeof(T);
            if (!types.Contains(t)) types.Add(t);
        }
        AddIfMissing<Events.DataReloadedEvent>();
        AddIfMissing<Events.FrameAdvancedEvent>();
        AddIfMissing<Events.InputReceivedEvent>();
        AddIfMissing<Events.InputBufferExpiredEvent>();
        AddIfMissing<Events.ChargeStateChangedEvent>();
        AddIfMissing<Events.MoveStartedEvent>();
        AddIfMissing<Events.MoveFrameChangedEvent>();
        AddIfMissing<Events.CancelWindowEnteredEvent>();
        AddIfMissing<Events.CancelWindowExitedEvent>();
        AddIfMissing<Events.HitConnectedEvent>();
        AddIfMissing<Events.MoveBlockedEvent>();
        AddIfMissing<Events.KnockbackAppliedEvent>();
        AddIfMissing<Events.StateChangedEvent>();
        AddIfMissing<Events.StateStackChangedEvent>();
        AddIfMissing<Events.ComboStartedEvent>();
        AddIfMissing<Events.MoveCanceledEvent>();
        AddIfMissing<Events.ComboEndedEvent>();
        AddIfMissing<Events.CharacterSelectedEvent>();
        AddIfMissing<Events.MatchInitializedEvent>();
        AddIfMissing<Events.FrameRewoundEvent>();
        AddIfMissing<Events.SceneChangingEvent>();
        AddIfMissing<Events.SceneChangedEvent>();
        AddIfMissing<Events.ReplayStartedEvent>();
        AddIfMissing<Events.ReplayEndedEvent>();
        AddIfMissing<Events.ReplayPausedEvent>();
        AddIfMissing<Events.StateRestoredEvent>();
        return types;
    }

    internal void BeginSnapshotQuiescence()
    {
        lock (_epochSync)
        {
            if (_dispatching || _snapshotQuiesced)
                throw new InvalidOperationException("[EventBus] Snapshot quiescence requires an idle EventBus.");
            _snapshotQuiesced = true;
        }
    }

    internal void EndSnapshotQuiescence(bool discardQuarantined)
    {
        lock (_epochSync)
        {
            if (!_snapshotQuiesced)
                throw new InvalidOperationException("[EventBus] Snapshot quiescence is not active.");
            if (!discardQuarantined)
                _currentQueue.AddRange(_quarantinedQueue.Where(e => e.Epoch == _lifecycleEpoch));
            _quarantinedQueue.Clear();
            _snapshotQuiesced = false;
        }
    }

    internal ulong ReserveLifecycleEpoch()
    {
        lock (_epochSync)
        {
            if (!_snapshotQuiesced || _reservedEpoch is not null)
                throw new InvalidOperationException("[EventBus] Epoch reservation requires one active snapshot Prepare.");
            _reservedEpoch = checked(_lifecycleEpoch + 1UL);
            return _reservedEpoch.Value;
        }
    }

    internal void CancelReservedEpoch() => _reservedEpoch = null;

    internal int PrepareRestoreCommit(ulong reservedEpoch, int completedFrame)
    {
        if (_reservedEpoch != reservedEpoch)
            throw new InvalidOperationException("[EventBus] Reserved epoch does not match.");
        if (completedFrame == int.MaxValue)
            throw new InvalidOperationException("[EventBus] Restored frame cannot advance beyond Int32.MaxValue.");
        return completedFrame + 1;
    }

    internal void CommitReservedEpoch(ulong reservedEpoch, int nextFrame)
    {
        _lifecycleEpoch = reservedEpoch;
        _reservedEpoch = null;
        _frameNumber = nextFrame;
        _currentQueue.Clear();
        _nextQueue.Clear();
        while (_pendingReloads.TryDequeue(out _)) { }
    }

    internal void PublishStateRestored(Events.StateRestoredEvent evt)
    {
        if (!_snapshotQuiesced)
            throw new InvalidOperationException("[EventBus] StateRestored requires snapshot quiescence.");
        var envelope = new Envelope(evt, _lifecycleEpoch, _frameNumber, NextEnvelopeSequence());
        _publicationProhibited = true;
        try { DispatchEnvelope(envelope, evt); }
        finally { _publicationProhibited = false; }
    }

    // Dispatches all queued events of type T in LIFO order (reverse iteration).
    // LIFO guarantees that when multiple events of the same type are published
    // within one frame, the most recent one fires first. This is load-bearing
    // for CancelWindowTracker: all CancelWindowEntered dispatch before any
    // CancelWindowExited within the same phase, because Entered is published
    // after Exited in the tracker's EvaluateFrame flow.
    private void DispatchType<T>() where T : struct
    {
        for (int i = _currentQueue.Count - 1; i >= 0; i--)
        {
            if (_currentQueue[i].Payload is T evt)
            {
                var envelope = _currentQueue[i];
                if (envelope.Epoch != _lifecycleEpoch)
                {
                    _currentQueue.RemoveAt(i);
                    continue;
                }
                if (IsLifecycle(evt))
                {
                    envelope = ActivateLifecycle(envelope);
                    DispatchEnvelope(envelope, evt);
                    return;
                }
                _currentQueue.RemoveAt(i);
                DispatchEnvelope(envelope, evt);
            }
        }
    }

    private void CoalesceDataReloads()
    {
        var latestByPath = new Dictionary<string, Envelope>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        for (int i = 0; i < _currentQueue.Count; i++)
        {
            if (_currentQueue[i].Payload is not Events.DataReloadedEvent reload ||
                _currentQueue[i].Epoch != _lifecycleEpoch) continue;
            string path;
            try { path = System.IO.Path.GetFullPath(reload.FilePath); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            { path = reload.FilePath; }
            latestByPath[path] = _currentQueue[i] with { Payload = new Events.DataReloadedEvent(path) };
        }
        _currentQueue.RemoveAll(static envelope => envelope.Payload is Events.DataReloadedEvent);
        // DispatchType is LIFO; reverse insertion yields deterministic canonical ascending dispatch.
        foreach (Envelope envelope in latestByPath.OrderByDescending(pair => pair.Key,
                     OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                 .Select(pair => pair.Value))
            _currentQueue.Add(envelope);
    }

    private static bool IsLifecycle<T>(T evt) =>
        evt is Events.MatchInitializedEvent or Events.ReplayStartedEvent or Events.ReplayEndedEvent;

    private Envelope ActivateLifecycle(Envelope envelope)
    {
        lock (_epochSync)
        {
            if (_lifecycleEpoch == ulong.MaxValue)
                throw new InvalidOperationException("[EventBus] Lifecycle epoch exhausted.");
            _lifecycleEpoch++;
            _currentQueue.RemoveAll(e => e.Epoch != _lifecycleEpoch);
            _nextQueue.RemoveAll(e => e.Epoch != _lifecycleEpoch);
            return envelope with { Epoch = _lifecycleEpoch };
        }
    }

    internal void SetLifecycleEpochForTesting(ulong epoch) => _lifecycleEpoch = epoch;

    internal void BeginReplayApply() => _replayApplyActive = true;
    internal void EndReplayApply() => _replayApplyActive = false;
    internal void BeginReplayAuthoritativeApply() => BeginReplayApply();
    internal void EndReplayAuthoritativeApply() => EndReplayApply();

    internal SnapshotAtomicityDiagnostic GetSnapshotAtomicityDiagnostic()
    {
        lock (_epochSync)
            return new SnapshotAtomicityDiagnostic(_lifecycleEpoch, _reservedEpoch,
                Describe(_currentQueue), Describe(_nextQueue), Describe(_pendingReloads.ToArray()),
                Describe(_quarantinedQueue));

        static IReadOnlyList<SnapshotEnvelopeDiagnostic> Describe(IEnumerable<Envelope> envelopes) =>
            envelopes.Select(e => new SnapshotEnvelopeDiagnostic(
                e.Payload.GetType().FullName ?? e.Payload.GetType().Name,
                e.Payload.ToString() ?? string.Empty, e.Frame, e.Epoch, e.Sequence)).ToArray();
    }

    private long NextEnvelopeSequence() => checked(++_nextEnvelopeSequence);

    internal EventBusTestDiagnostic BeginTestScope()
    {
        lock (_epochSync)
        {
            int threadId = Environment.CurrentManagedThreadId;
            if (_dispatching)
                throw new InvalidOperationException("[EventBus] Cannot begin a test scope during dispatch.");
            if (_testOwnerThreadId is not null)
                throw new InvalidOperationException("[EventBus] A test scope is already active.");

            _testOwnerThreadId = threadId;
            ResetTestState();
            return GetTestDiagnosticCore();
        }
    }

    internal EventBusTestDiagnostic EndTestScope()
    {
        lock (_epochSync)
        {
            EnsureTestOwner();
            if (_dispatching)
                throw new InvalidOperationException("[EventBus] Cannot end a test scope during dispatch.");

            EventBusTestDiagnostic residual = GetTestDiagnosticCore();
            ResetTestState();
            _testOwnerThreadId = null;
            return residual;
        }
    }

    internal EventBusTestDiagnostic GetTestDiagnostic()
    {
        lock (_epochSync)
            return GetTestDiagnosticCore();
    }

    private void EnsureTestOwner()
    {
        if (_testOwnerThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("[EventBus] Test state may only be changed by the owning test thread.");
    }

    private void ResetTestState()
    {
        _subscribers.Clear();
        _currentQueue.Clear();
        _nextQueue.Clear();
        _quarantinedQueue.Clear();
        while (_pendingReloads.TryDequeue(out _)) { }
        _dispatching = false;
        _frameNumber = 0;
        _dispatchFrame = 0;
        _dispatchEpoch = null;
        _reservedEpoch = null;
        _snapshotQuiesced = false;
        _publicationProhibited = false;
        _replayApplyActive = false;
        _nextEnvelopeSequence = 0;
        Paused = false;
        StepRequested = false;
        SuppressFrameAdvanced = false;
        Recorder = null;

        long generation = checked(++_testGeneration);
        _lifecycleEpoch = checked((ulong)generation + 1UL);
    }

    private EventBusTestDiagnostic GetTestDiagnosticCore()
    {
        var subscriberTypes = _subscribers
            .Where(pair => pair.Value.Count > 0)
            .Select(pair => new EventBusSubscriberDiagnostic(pair.Key.FullName ?? pair.Key.Name, pair.Value.Count))
            .OrderBy(item => item.EventType, StringComparer.Ordinal)
            .ToArray();

        return new EventBusTestDiagnostic(
            subscriberTypes,
            subscriberTypes.Sum(item => item.Count),
            _currentQueue.Count,
            _nextQueue.Count,
            _pendingReloads.Count,
            _frameNumber,
            _dispatchFrame,
            _lifecycleEpoch,
            checked((ulong)_testGeneration),
            _dispatching,
            _dispatchEpoch,
            Paused,
            StepRequested,
            SuppressFrameAdvanced,
            _replayApplyActive,
            Recorder is not null);
    }
}

internal sealed record EventBusSubscriberDiagnostic(string EventType, int Count);

internal sealed record SnapshotEnvelopeDiagnostic(
    string PayloadType, string PayloadValue, int Frame, ulong Epoch, long Sequence);

internal sealed record SnapshotAtomicityDiagnostic(
    ulong ActiveEpoch,
    ulong? ReservedEpoch,
    IReadOnlyList<SnapshotEnvelopeDiagnostic> Current,
    IReadOnlyList<SnapshotEnvelopeDiagnostic> Next,
    IReadOnlyList<SnapshotEnvelopeDiagnostic> Pending,
    IReadOnlyList<SnapshotEnvelopeDiagnostic> Quarantined);

internal sealed record EventBusTestDiagnostic(
    IReadOnlyList<EventBusSubscriberDiagnostic> SubscriberTypes,
    int SubscriberCount,
    int CurrentQueueCount,
    int NextQueueCount,
    int PendingReloadCount,
    int FrameNumber,
    int DispatchFrame,
    ulong LifecycleEpoch,
    ulong TestGeneration,
    bool IsDispatching,
    ulong? DispatchEpoch,
    bool Paused,
    bool StepRequested,
    bool SuppressFrameAdvanced,
    bool ReplayApplyActive,
    bool HasRecorder)
{
    public int SubscriberTypeCount => SubscriberTypes.Count;

    public bool HasResidualState => SubscriberTypeCount != 0
        || SubscriberCount != 0
        || CurrentQueueCount != 0
        || NextQueueCount != 0
        || PendingReloadCount != 0
        || IsDispatching
        || DispatchEpoch is not null
        || Paused
        || StepRequested
        || SuppressFrameAdvanced
        || ReplayApplyActive
        || HasRecorder;

    public bool IsClean => SubscriberTypeCount == 0
        && SubscriberCount == 0
        && CurrentQueueCount == 0
        && NextQueueCount == 0
        && PendingReloadCount == 0
        && FrameNumber == 0
        && DispatchFrame == 0
        && !IsDispatching
        && DispatchEpoch is null
        && !Paused
        && !StepRequested
        && !SuppressFrameAdvanced
        && !ReplayApplyActive
        && !HasRecorder;
}
