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
    public int Phase { get; init; }
    public int Sequence { get; init; }
    public ulong SourceEpoch { get; init; }

    public ReplayEntry(int frame, string eventType, string payload, int phase = 0, int sequence = 0, ulong sourceEpoch = 0)
    {
        Frame = frame >= 0
            ? frame
            : throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frame must be non-negative.");
        EventType = string.IsNullOrWhiteSpace(eventType)
            ? throw new ArgumentException("EventType must not be empty.", nameof(eventType))
            : eventType;
        Payload = payload ?? string.Empty;
        Phase = phase is >= 0 and <= 7
            ? phase
            : throw new ArgumentOutOfRangeException(nameof(phase));
        Sequence = sequence >= 0
            ? sequence
            : throw new ArgumentOutOfRangeException(nameof(sequence));
        SourceEpoch = sourceEpoch;
    }
}
