using System;
using System.Collections.Generic;

namespace FTG_Framework.Core;

public sealed class EventBus
{
    private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
    
    public static EventBus Instance => _instance.Value;

    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly List<object> _currentQueue = new();
    private readonly List<object> _nextQueue = new();
    private bool _dispatching;
    private int _frameNumber;

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

    public void Publish<T>(T evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));

        if (_dispatching)
            _nextQueue.Add(evt);
        else
            _currentQueue.Add(evt);
    }

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

            // Phase 5: UI — read-only observer, no events to dispatch

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
