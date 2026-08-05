#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Production player — reads a ReplayFile and provides events frame by frame.
/// Injections queue through EventBus.InjectReplayEvent so effects land in the
/// frame dispatch pipeline at the recorded position (end of frame k, visible in
/// the hash at k+1), matching the recording side. The owner-applier hook
/// reproduces direct engine calls (e.g. StartMove) performed on the live side
/// outside the event stream.
/// </summary>
internal sealed class ReplayPlayer : IReplayPlayer
{
    private readonly Dictionary<int, List<ReplayEntry>> _eventsByFrame = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Cached: event type name → Type for deserialization
    private readonly Dictionary<string, Type> _dispatchCache = new();
    private ReplayFile? _loadedFile;

    /// <summary>
    /// Reproduces direct engine calls that the recording side performed outside
    /// the event stream (e.g. FrameDataEngine.StartMove for MoveStartedEvent).
    /// Invoked at injection time, before the event is queued.
    /// </summary>
    public Action<object>? OwnerApplier { get; set; }

    public int FrameCount { get; private set; }
    public int TotalEvents { get; private set; }
    public bool IsPlaying { get; set; }

    public void Load(ReplayFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Entries is null)
            throw new ArgumentException("Entries must not be null.", nameof(file));

        ReplayVersionValidator.ValidateVersion(file.DataVersion);
        foreach (var entry in file.Entries)
        {
            ReplayVersionValidator.ValidateEntryPayload(file.DataVersion, entry);
            Type? registeredType = EventTypeRegistry.Resolve(entry.EventType);
            if (registeredType is null || (entry.Phase != 0 && EventTypeRegistry.GetPhase(registeredType) != entry.Phase))
                throw new ArgumentException($"[Replay] Event '{entry.EventType}' has invalid phase {entry.Phase}.", nameof(file));
        }

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

            // Pre-cache event type for deserialization
            if (!_dispatchCache.ContainsKey(entry.EventType))
            {
                Type? eventType = EventTypeRegistry.Resolve(entry.EventType);
                if (eventType is not null)
                    _dispatchCache[entry.EventType] = eventType;
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
    /// Drains all events recorded at the given frame: owner-applies direct
    /// engine calls, then queues the envelope for the frame dispatch pipeline.
    /// Returns the number of events successfully queued.
    /// </summary>
    public int ProcessFrameReplay(EventBus bus, int frameNumber)
    {
        var entries = GetEventsForFrame(frameNumber);
        if (entries.Count == 0)
            return 0;

        int dispatched = 0;
        // ReplayRecorder assigns (frame, phase, seq) in DISPATCH order; ProcessFrame
        // dispatches per-phase LIFO, so queue in descending order to reproduce it.
        foreach (var entry in entries.OrderByDescending(entry => entry.Phase == 0
                     ? EventTypeRegistry.GetPhase(EventTypeRegistry.Resolve(entry.EventType)!)
                     : entry.Phase).ThenByDescending(entry => entry.Sequence))
        {
            if (!_dispatchCache.TryGetValue(entry.EventType, out Type? eventType))
                continue;

            var eventObj = JsonSerializer.Deserialize(entry.Payload, eventType, JsonOptions);
            if (eventObj is null)
                continue;

            ReplayVersionValidator.ValidateDeserializedEvent(eventObj, entry.EventType);
            OwnerApplier?.Invoke(eventObj);
            bus.InjectReplayEvent(eventObj);
            dispatched++;
        }

        return dispatched;
    }
}
