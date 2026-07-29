#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Core.Replay;

/// <summary>
/// Serialized replay file format. Immutable after deserialization.
/// </summary>
public sealed class ReplayFile
{
    public string FrameworkVersion { get; init; }
    public int DataVersion { get; init; }
    public int FrameCount { get; init; }
    public IReadOnlyList<ReplayEntry> Entries { get; init; }

    public int EventCount => Entries.Count;

    public ReplayFile(string frameworkVersion, int dataVersion, int frameCount, IReadOnlyList<ReplayEntry> entries)
    {
        FrameworkVersion = string.IsNullOrWhiteSpace(frameworkVersion)
            ? throw new ArgumentException("FrameworkVersion must not be empty.", nameof(frameworkVersion))
            : frameworkVersion;
        DataVersion = dataVersion >= 1
            ? dataVersion
            : throw new ArgumentOutOfRangeException(nameof(dataVersion), dataVersion, "DataVersion must be >= 1.");
        FrameCount = frameCount >= 0
            ? frameCount
            : throw new ArgumentOutOfRangeException(nameof(frameCount), frameCount, "FrameCount must be non-negative.");
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }
}
