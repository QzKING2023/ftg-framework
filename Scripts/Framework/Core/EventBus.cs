using System;
using System.Collections.Generic;

namespace FTG_Framework.Core;

/// <summary>
/// Process-global, single-threaded event bus with a 5-phase frame pipeline.
///
/// <b>AD-4 Dispatch Semantics</b>
///
/// <b>1. LIFO same-type dispatch.</b> Within a single dispatch phase, events of the
/// same type are dispatched in last-in-first-out order — the most recently published
/// event fires first. This matters when the same subscriber publishes multiple events
/// of the same type during one frame (e.g., all CancelWindowEntered fire before any
/// CancelWindowExited within the same phase group).
///
/// <b>2. Per-phase grouping.</b> <c>ProcessFrame()</c> partitions dispatch into five
/// ordered phases. All events of a given type are dispatched together before moving
/// to the next type. Types within a phase are dispatched in the order listed in
/// <c>ProcessFrame()</c>.
///
/// <b>3. Same-frame response queuing.</b> If a subscriber publishes an event while
/// a dispatch is already in progress, the new event is queued in <c>_nextQueue</c>
/// and dispatched during the following frame. This prevents unbounded re-entry and
/// keeps frame boundaries well-defined.
///
/// <b>Five-phase pipeline:</b>
/// <list type="number">
/// <item>Phase 1 — Frame tick: <c>FrameAdvancedEvent</c> (auto-injected)</item>
/// <item>Phase 2 — Input System: <c>InputReceivedEvent</c>, <c>InputBufferExpiredEvent</c>, <c>ChargeStateChangedEvent</c></item>
/// <item>Phase 3 — Frame Data Engine: <c>MoveFrameChangedEvent</c>, <c>CancelWindowEnteredEvent</c>, <c>CancelWindowExitedEvent</c>, <c>HitConnectedEvent</c>, <c>MoveBlockedEvent</c></item>
/// <item>Phase 4 — Combo Exec: <c>ComboStartedEvent</c>, <c>MoveCanceledEvent</c>, <c>ComboEndedEvent</c></item>
/// <item>Phase 5 — UI: <c>CharacterSelectedEvent</c>, <c>MatchInitializedEvent</c> (character select flow)</item>
/// </list>
/// </summary>
public sealed class EventBus
{
    private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
    
    public static EventBus Instance => _instance.Value;

    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly List<object> _currentQueue = new();
    private readonly List<object> _nextQueue = new();
    private bool _dispatching;
    private int _frameNumber;

    public int CurrentFrame => _frameNumber;

    public bool Paused { get; set; }
    public bool StepRequested { get; set; }

    // Rewinds after a state restore: the next processed frame re-executes as
    // nextFrame, keeping snapshots, input history, and displays in one frame
    // domain. Queued events from the abandoned future are purged.
    internal void RewindFrameCounter(int nextFrame)
    {
        _frameNumber = nextFrame;
        _currentQueue.Clear();
        _nextQueue.Clear();
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

    // 5-phase dispatch pipeline. Each phase dispatches all queued events of its
    // declared types before the next phase begins. The ordering is fixed:
    //   1. Frame tick      (FrameAdvanced — auto-injected)
    //   2. Input System    (InputReceived, InputBufferExpired, ChargeStateChanged)
    //   3. Frame Data Engine (MoveFrameChanged, CancelWindow*, HitConnected, MoveBlocked)
    //   4. Combo Exec      (ComboStarted, MoveCanceled, ComboEnded)
    //   5. UI              (read-only observer; no events dispatched here)
    // This ordering guarantees that downstream systems see the upstream
    // system's events before their own subscribers run.
    public void ProcessFrame()
    {
        _dispatching = true;
        try
        {
            // Phase 1: Frame tick
            _currentQueue.Add(new Events.FrameAdvancedEvent(_frameNumber++));
            DispatchType<Events.FrameAdvancedEvent>();

            // Phase 2: Input System events
            DispatchType<Events.InputReceivedEvent>();
            DispatchType<Events.InputBufferExpiredEvent>();
            DispatchType<Events.ChargeStateChangedEvent>();

            // Phase 3: Frame Data Engine events
            DispatchType<Events.MoveFrameChangedEvent>();
            DispatchType<Events.CancelWindowEnteredEvent>();
            DispatchType<Events.CancelWindowExitedEvent>();
            DispatchType<Events.HitConnectedEvent>();
            DispatchType<Events.MoveBlockedEvent>();

            // Phase 4: Combo Exec events
            DispatchType<Events.ComboStartedEvent>();
            DispatchType<Events.MoveCanceledEvent>();
            DispatchType<Events.ComboEndedEvent>();

            // Phase 5: UI — read-only observer
            DispatchType<Events.CharacterSelectedEvent>();
            DispatchType<Events.MatchInitializedEvent>();

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
