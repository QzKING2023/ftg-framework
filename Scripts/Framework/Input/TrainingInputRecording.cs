#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using FTG_Framework.Core;

namespace FTG_Framework.Input;

public sealed class TrainingInputRecordingException : Exception
{
    public TrainingInputRecordingException(string message) : base(message) { }
    public TrainingInputRecordingException(string message, Exception inner) : base(message, inner) { }
}

public readonly record struct TrainingInputRecordingEntry(
    int RelativeFrame,
    int WithinFrameSequence,
    InputType InputType,
    int InputValue);

public sealed class TrainingInputRecording
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxDurationFrames = 36_000;
    public const int MaxEntries = 65_536;
    public const int MaxEncodedBytes = 4 * 1024 * 1024;
    public const int MaxNameScalars = 128;

    private readonly ReadOnlyCollection<TrainingInputRecordingEntry> _entries;

    public int SchemaVersion { get; }
    public int SourcePlayer { get; }
    public int DurationFrames { get; }
    public string Name { get; }
    public IReadOnlyList<TrainingInputRecordingEntry> Entries => _entries;

    public TrainingInputRecording(int schemaVersion, int sourcePlayer, int durationFrames,
        string? name, IEnumerable<TrainingInputRecordingEntry> entries)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw Error($"Unsupported schema version {schemaVersion}.");
        if (sourcePlayer is < 1 or > 2)
            throw Error($"Source player must be 1 or 2, got {sourcePlayer}.");
        if (durationFrames is < 0 or > MaxDurationFrames)
            throw Error($"Duration must be between 0 and {MaxDurationFrames}, got {durationFrames}.");

        Name = name ?? string.Empty;
        ValidateName(Name);

        ArgumentNullException.ThrowIfNull(entries);
        var copy = entries.Take(MaxEntries + 1).ToArray();
        if (copy.Length > MaxEntries)
            throw Error($"Entry count exceeds {MaxEntries}.");
        ValidateEntries(copy, durationFrames);

        SchemaVersion = schemaVersion;
        SourcePlayer = sourcePlayer;
        DurationFrames = durationFrames;
        _entries = Array.AsReadOnly(copy);
    }

    public static void ValidateName(string? name)
    {
        if ((name ?? string.Empty).EnumerateRunes().Count() > MaxNameScalars)
            throw Error($"Name exceeds {MaxNameScalars} Unicode scalar values.");
    }

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name ?? string.Empty).EnumerateRunes().Count() <= MaxNameScalars;

    private static void ValidateEntries(TrainingInputRecordingEntry[] entries, int duration)
    {
        int priorFrame = -1;
        int expectedSequence = 0;
        foreach (var entry in entries)
        {
            if (entry.RelativeFrame < 0 || entry.RelativeFrame > duration)
                throw Error($"Relative frame {entry.RelativeFrame} is outside duration {duration}.");
            if (entry.RelativeFrame < priorFrame)
                throw Error("Entries must be ordered by relative frame.");
            if (entry.RelativeFrame != priorFrame)
            {
                priorFrame = entry.RelativeFrame;
                expectedSequence = 0;
            }
            if (entry.WithinFrameSequence != expectedSequence)
                throw Error($"Within-frame sequence must be contiguous from zero at frame {entry.RelativeFrame}.");
            expectedSequence = checked(expectedSequence + 1);
            if (!Enum.IsDefined(entry.InputType))
                throw Error($"Unsupported input type {(int)entry.InputType}.");
            if (entry.InputType == InputType.Directional &&
                !Enum.IsDefined(typeof(DirectionValue), entry.InputValue))
                throw Error($"Unsupported directional value {entry.InputValue}.");
            if (entry.InputType == InputType.Button &&
                !Enum.IsDefined(typeof(ButtonValue), entry.InputValue))
                throw Error($"Unsupported button value {entry.InputValue}.");
        }
    }

    private static TrainingInputRecordingException Error(string message) =>
        new($"[Input] {message}");
}
