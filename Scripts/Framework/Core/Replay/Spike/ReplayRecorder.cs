#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FTG_Framework.Core.Replay.Spike;

// ============================================================================
// Task 3: Minimal reference recording + replay for spike validation
// Spike code — throwaway, not for production use.
// ============================================================================

/// <summary>
/// Records events with frame-number tags for replay. Spike minimal implementation.
/// </summary>
public interface IReplayRecorder
{
    void Record<T>(int frame, T evt);
    ReplayFile Save();
    int MaxFrameNumber { get; }
    int EventCount { get; }
}

/// <summary>
/// Plays back recorded events frame by frame. Spike minimal implementation.
/// </summary>
public interface IReplayPlayer
{
    void Load(ReplayFile file);
    IReadOnlyList<ReplayEntry> GetEventsForFrame(int frameNumber);
    int FrameCount { get; }
    int TotalEvents { get; }
}

/// <summary>
/// Serialized replay file format (AC 4.1 spec).
/// </summary>
public sealed class ReplayFile
{
    public string FrameworkVersion { get; set; } = "2.3.0-spike";
    public int DataVersion { get; set; } = 1;
    public int FrameCount { get; set; }
    public int EventCount => Entries.Count;
    public List<ReplayEntry> Entries { get; set; } = new();
}

/// <summary>
/// Single event entry in the replay stream: { frame, typeName, payload }.
/// </summary>
public sealed class ReplayEntry
{
    public int Frame { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// Minimal recorder — writes JSON entries in dispatch order.
/// In production this would hook into EventBus.DispatchType<T>() and PublishImmediate<T>().
/// For the spike, it records events explicitly passed to it.
/// </summary>
public sealed class ReplayRecorder : IReplayRecorder
{
    private readonly List<ReplayEntry> _entries = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>Max frame number seen — not a count of distinct frames.</summary>
    public int MaxFrameNumber { get; private set; }
    public int EventCount => _entries.Count;

    public void Record<T>(int frame, T evt)
    {
        if (frame < 0)
            throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frame must be non-negative.");
        var typeName = typeof(T).Name;
        var payload = JsonSerializer.Serialize(evt, typeof(T), JsonOptions);
        _entries.Add(new ReplayEntry
        {
            Frame = frame,
            EventType = typeName,
            Payload = payload
        });
        if (frame > MaxFrameNumber)
            MaxFrameNumber = frame;
    }

    public ReplayFile Save()
    {
        var copied = new List<ReplayEntry>(_entries.Count);
        foreach (var e in _entries)
            copied.Add(new ReplayEntry { Frame = e.Frame, EventType = e.EventType, Payload = e.Payload });
        return new ReplayFile
        {
            FrameworkVersion = "2.3.0-spike",
            DataVersion = 1,
            FrameCount = MaxFrameNumber,
            Entries = copied
        };
    }
}

/// <summary>
/// Minimal player — reads a ReplayFile and provides events frame by frame.
/// Replay injection should call ProcessFrameReplay(frameNumber) which drains
/// all events recorded at that frame via PublishImmediate, then advances.
/// </summary>
public sealed class ReplayPlayer : IReplayPlayer
{
    private readonly Dictionary<int, List<ReplayEntry>> _eventsByFrame = new();

    public int FrameCount { get; private set; }
    public int TotalEvents { get; private set; }

    public void Load(ReplayFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Entries is null)
            throw new ArgumentException("Entries must not be null.", nameof(file));
        _eventsByFrame.Clear();
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
        }
    }

    public IReadOnlyList<ReplayEntry> GetEventsForFrame(int frameNumber)
    {
        return _eventsByFrame.TryGetValue(frameNumber, out var list)
            ? list.AsReadOnly()
            : System.Array.Empty<ReplayEntry>();
    }
}
