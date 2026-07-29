#nullable enable
using System;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Single event entry in the replay stream: { Frame, EventType, Payload }.
/// </summary>
public sealed class ReplayEntry
{
    public int Frame { get; init; }
    public string EventType { get; init; }
    public string Payload { get; init; }

    public ReplayEntry(int frame, string eventType, string payload)
    {
        Frame = frame >= 0
            ? frame
            : throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frame must be non-negative.");
        EventType = string.IsNullOrWhiteSpace(eventType)
            ? throw new ArgumentException("EventType must not be empty.", nameof(eventType))
            : eventType;
        Payload = payload ?? string.Empty;
    }
}
