#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
/// <b>Eight-phase pipeline (AD-12):</b>
/// <list type="number">
/// <item>Phase 0 — Hot-Reload: drain <c>DataReloadedEvent</c> from FileWatcher</item>
/// <item>Phase 1 — Frame tick: <c>FrameAdvancedEvent</c> (auto-injected)</item>
/// <item>Phase 2 — Input System: <c>InputReceivedEvent</c>, <c>InputBufferExpiredEvent</c>, <c>ChargeStateChangedEvent</c></item>
/// <item>Phase 3 — Frame Data Engine: <c>MoveFrameChangedEvent</c>, <c>CancelWindowEnteredEvent</c>, <c>CancelWindowExitedEvent</c>, <c>MoveStartedEvent</c></item>
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
    private readonly List<object> _currentQueue = new();
    private readonly List<object> _nextQueue = new();
    private readonly ConcurrentQueue<Events.DataReloadedEvent> _pendingReloads = new();
    private bool _dispatching;
    private int _frameNumber;

    public int CurrentFrame => _frameNumber;

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
        _pendingReloads.Enqueue(evt);
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
            handlers.Remove(handler);
    }

    // Events published outside a dispatch go to _currentQueue (processed this frame).
    // Events published DURING a dispatch (re-entrant publish) go to _nextQueue and
    // are held until the next frame — this is the same-frame response queuing rule.
    public void Publish<T>(T evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));

        if (_dispatching)
            _nextQueue.Add(evt);
        else
            _currentQueue.Add(evt);
    }

    // Bypasses the frame queue and dispatches to subscribers synchronously. Required
    // for rewind/restore events: while paused, ProcessFrame never runs, so queued
    // events would either never arrive or arrive stale (LIFO) on resume.
    public void PublishImmediate<T>(T evt) where T : struct
    {
        // Record rewind/restore events that bypass the queue.
        // IMPORTANT: During replay playback, Recorder must be null to prevent
        // double-recording of injected events. See ReplayOrchestrator.
        Recorder?.Record(_dispatchFrame, evt);

        if (_subscribers.TryGetValue(typeof(T), out var handlers))
        {
            var snapshot = handlers.ToArray();
            foreach (var handler in snapshot)
            {
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
    }

    // 8-phase dispatch pipeline (AD-12). Each phase dispatches all queued events
    // of its declared types before the next phase begins. The ordering is fixed:
    //   0. Hot-Reload      (DataReloaded — drained from FileWatcher)
    //   1. Frame tick      (FrameAdvanced — auto-injected)
    //   2. Input System    (InputReceived, InputBufferExpired, ChargeStateChanged)
    //   3. Frame Data Engine (MoveFrameChanged, CancelWindow*, MoveStarted)
    //   4. Physics         (HitConnected, MoveBlocked, KnockbackApplied)
    //   5. State Machine   (StateChanged, StateStackChanged)
    //   6. Combo System    (ComboStarted, MoveCanceled, ComboEnded)
    //   7. UI              (CharacterSelected, MatchInitialized, Scene*, Replay*)
    // This ordering guarantees that downstream systems see the upstream
    // system's events before their own subscribers run.
    public void ProcessFrame()
    {
        _dispatching = true;
        _dispatchFrame = _frameNumber;
        try
        {
            // Phase 0: Hot-Reload — drain DataReloadedEvent from FileWatcher
            while (_pendingReloads.TryDequeue(out var reloadEvt))
                _currentQueue.Add(reloadEvt);
            DispatchType<Events.DataReloadedEvent>();

            // Phase 1: Frame tick
            if (!SuppressFrameAdvanced)
                _currentQueue.Add(new Events.FrameAdvancedEvent(_frameNumber));
            _frameNumber++;
            DispatchType<Events.FrameAdvancedEvent>();

            // Phase 2: Input System events
            DispatchType<Events.InputReceivedEvent>();
            DispatchType<Events.InputBufferExpiredEvent>();
            DispatchType<Events.ChargeStateChangedEvent>();

            // Phase 3: Frame Data Engine events
            DispatchType<Events.MoveFrameChangedEvent>();
            DispatchType<Events.CancelWindowEnteredEvent>();
            DispatchType<Events.CancelWindowExitedEvent>();
            DispatchType<Events.MoveStartedEvent>();

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
        AddIfMissing<Events.MoveFrameChangedEvent>();
        AddIfMissing<Events.CancelWindowEnteredEvent>();
        AddIfMissing<Events.CancelWindowExitedEvent>();
        AddIfMissing<Events.MoveStartedEvent>();
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
        return types;
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
            if (_currentQueue[i] is T evt)
            {
                _currentQueue.RemoveAt(i);
                Recorder?.Record(_dispatchFrame, evt);
                if (_subscribers.TryGetValue(typeof(T), out var handlers))
                {
                    var snapshot = handlers.ToArray();
                    foreach (var handler in snapshot)
                    {
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
            }
        }
    }
}
