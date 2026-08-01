#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Production recorder — writes JSON entries in dispatch order.
/// Thread-safe for concurrent Record calls from different threads.
/// </summary>
internal sealed class ReplayRecorder : IReplayRecorder
{
    private readonly List<ReplayEntry> _entries = new();
    private readonly object _lock = new();
    private int _sequenceFrame = -1;
    private readonly Dictionary<int, int> _phaseSequences = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public int MaxFrameNumber { get; private set; }
    public int EventCount => _entries.Count;
    public bool IsRecording { get; set; }

    public void Record<T>(int frame, T evt)
    {
        if (!IsRecording)
            return;

        if (frame < 0)
            throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frame must be non-negative.");

        var typeName = EventTypeRegistry.GetName(typeof(T));
        var payload = JsonSerializer.Serialize(evt, typeof(T), JsonOptions);
        lock (_lock)
        {
            if (_sequenceFrame != frame)
            {
                _sequenceFrame = frame;
                _phaseSequences.Clear();
            }
            int phase = EventTypeRegistry.GetPhase(typeof(T));
            int sequence = _phaseSequences.TryGetValue(phase, out int current) ? current : 0;
            _phaseSequences[phase] = checked(sequence + 1);
            var entry = new ReplayEntry(frame, typeName, payload, phase, sequence, EventBus.Instance.LifecycleEpoch);
            _entries.Add(entry);
            if (frame > MaxFrameNumber)
                MaxFrameNumber = frame;
        }
    }

    public ReplayFile Save()
    {
        FrameworkLog.Info?.Invoke($"[Replay] Saving recording: {_entries.Count} events over {MaxFrameNumber} frames.");

        List<ReplayEntry> copied;
        lock (_lock)
        {
            copied = new List<ReplayEntry>(_entries.Count);
            foreach (var e in _entries)
                copied.Add(new ReplayEntry(e.Frame, e.EventType, e.Payload, e.Phase, e.Sequence, e.SourceEpoch));
        }

        return new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, MaxFrameNumber + 1, copied);
    }
}
