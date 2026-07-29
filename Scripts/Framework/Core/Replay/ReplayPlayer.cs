#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Production player — reads a ReplayFile and provides events frame by frame.
/// Uses cached MethodInfo delegates for per-event PublishImmediate dispatch
/// (zero per-frame reflection overhead).
/// </summary>
internal sealed class ReplayPlayer : IReplayPlayer
{
    private readonly Dictionary<int, List<ReplayEntry>> _eventsByFrame = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MethodInfo PublishImmediateMethod = typeof(EventBus)
        .GetMethod(nameof(EventBus.PublishImmediate), BindingFlags.Public | BindingFlags.Instance)!;

    // Cached: event type name → (MethodInfo, Type) for PublishImmediate<T> dispatch
    private readonly Dictionary<string, (MethodInfo method, Type eventType)> _dispatchCache = new();
    private ReplayFile? _loadedFile;

    public int FrameCount { get; private set; }
    public int TotalEvents { get; private set; }
    public bool IsPlaying { get; set; }

    public void Load(ReplayFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Entries is null)
            throw new ArgumentException("Entries must not be null.", nameof(file));

        _eventsByFrame.Clear();
        _dispatchCache.Clear();
        _loadedFile = file;
        FrameCount = file.FrameCount;
        TotalEvents = file.EventCount;

        foreach (var entry in file.Entries)
        {
            if (!_eventsByFrame.TryGetValue(entry.Frame, out var list))
            {
                list = new List<ReplayEntry>();
                _eventsByFrame[entry.Frame] = list;
            }
            list.Add(entry);

            // Pre-cache dispatch MethodInfo for this event type
            if (!_dispatchCache.ContainsKey(entry.EventType))
            {
                var eventType = EventTypeRegistry.Resolve(entry.EventType);
                if (eventType is not null)
                {
                    var genericMethod = PublishImmediateMethod.MakeGenericMethod(eventType);
                    _dispatchCache[entry.EventType] = (genericMethod, eventType);
                }
            }
        }
    }

    public IReadOnlyList<ReplayEntry> GetEventsForFrame(int frameNumber)
    {
        return _eventsByFrame.TryGetValue(frameNumber, out var list)
            ? list.AsReadOnly()
            : Array.Empty<ReplayEntry>();
    }

    /// <summary>
    /// Drains all events recorded at the given frame and injects them via PublishImmediate.
    /// Returns the number of events successfully dispatched.
    /// </summary>
    public int ProcessFrameReplay(EventBus bus, int frameNumber)
    {
        var entries = GetEventsForFrame(frameNumber);
        if (entries.Count == 0)
            return 0;

        int dispatched = 0;
        foreach (var entry in entries)
        {
            if (!_dispatchCache.TryGetValue(entry.EventType, out var cached))
                continue;

            var eventObj = JsonSerializer.Deserialize(entry.Payload, cached.eventType, JsonOptions);
            if (eventObj is null)
                continue;

            ReplayVersionValidator.ValidateDeserializedEvent(eventObj, entry.EventType);

            cached.method.Invoke(bus, [eventObj]);
            dispatched++;
        }

        return dispatched;
    }
}
