#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FTG_Framework.Core;

public sealed class EventDebugEntry
{
    public string EventTypeName { get; init; } = "";
    public int SubscriberCount { get; set; }
    public int LastFrameSeen { get; set; } = -1;
    public int TotalOccurrences { get; set; }
    public string LastPayloadSnapshot { get; set; } = "";
    public DateTime LastPayloadTimestamp { get; set; }
}

public sealed class EventBusDebugService
{
    private readonly Dictionary<string, EventDebugEntry> _entries = new();
    private readonly Dictionary<Type, int> _rateLimitCounters = new();
    private readonly Dictionary<Type, Delegate> _activeHandlers = new();
    private const int FrameAdvancedRateLimit = 15;

    public bool Enabled { get; private set; }

    public IReadOnlyList<EventDebugEntry> GetEntries()
    {
        lock (_entries)
        {
            return _entries.Values
                .OrderByDescending(e => e.LastFrameSeen)
                .ThenBy(e => e.EventTypeName)
                .ToList();
        }
    }

    public void Enable()
    {
        if (Enabled)
            return;

        var knownTypes = EventBus.Instance.GetKnownEventTypes();
        var subscribeMethod = typeof(EventBusDebugService)
            .GetMethod(nameof(SubscribeTo), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var eventType in knownTypes)
        {
            var typedMethod = subscribeMethod.MakeGenericMethod(eventType);
            var handler = Delegate.CreateDelegate(
                typeof(Action<>).MakeGenericType(eventType), this, typedMethod);
            _activeHandlers[eventType] = handler;

            var subscribeGeneric = typeof(EventBus)
                .GetMethod(nameof(EventBus.Subscribe), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(eventType);
            subscribeGeneric.Invoke(EventBus.Instance, new object[] { handler });

            var typeName = eventType.Name;
            if (!_entries.ContainsKey(typeName))
            {
                _entries[typeName] = new EventDebugEntry
                {
                    EventTypeName = typeName
                };
            }
            _entries[typeName].SubscriberCount = GetSubscriberCountFor(eventType);
        }

        Enabled = true;
    }

    public void Disable()
    {
        if (!Enabled)
            return;

        foreach (var (eventType, handler) in _activeHandlers)
        {
            var unsubscribeGeneric = typeof(EventBus)
                .GetMethod(nameof(EventBus.Unsubscribe), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(eventType);
            unsubscribeGeneric.Invoke(EventBus.Instance, new object[] { handler });
        }

        _activeHandlers.Clear();
        _entries.Clear();
        _rateLimitCounters.Clear();
        Enabled = false;
    }

    private void SubscribeTo<T>(T evt) where T : struct
    {
        var typeName = typeof(T).Name;
        var frameNumber = EventBus.Instance.CurrentFrame;

        lock (_entries)
        {
            if (!_entries.TryGetValue(typeName, out var entry))
            {
                entry = new EventDebugEntry { EventTypeName = typeName };
                _entries[typeName] = entry;
            }

            entry.TotalOccurrences++;
            entry.LastFrameSeen = frameNumber;
            entry.LastPayloadTimestamp = DateTime.UtcNow;
            entry.SubscriberCount = GetSubscriberCountFor(typeof(T));

            var isFrameAdvanced = typeof(T) == typeof(Events.FrameAdvancedEvent);
            if (!isFrameAdvanced)
            {
                entry.LastPayloadSnapshot = evt.ToString() ?? "";
            }
            else
            {
                if (!_rateLimitCounters.TryGetValue(typeof(T), out var counter))
                    counter = 0;
                counter++;
                if (counter >= FrameAdvancedRateLimit)
                {
                    entry.LastPayloadSnapshot = evt.ToString() ?? "";
                    counter = 0;
                }
                _rateLimitCounters[typeof(T)] = counter;
            }
        }
    }

    private static readonly Dictionary<Type, MethodInfo> _subscriberCountMethods = new();

    private static int GetSubscriberCountFor(Type eventType)
    {
        if (!_subscriberCountMethods.TryGetValue(eventType, out var method))
        {
            method = typeof(EventBus)
                .GetMethod(nameof(EventBus.GetSubscriberCount), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(eventType);
            _subscriberCountMethods[eventType] = method;
        }
        return (int)method.Invoke(EventBus.Instance, null)!;
    }
}
