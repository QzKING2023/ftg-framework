#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

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
    /// <summary>Canonical AD-20 initial snapshot bytes; null only for accepted legacy v1-v3 files.</summary>
    public byte[]? InitialSnapshot { get; init; }
    /// <summary>Lowercase-hex SHA-256 over the embedded <see cref="InitialSnapshot"/> bytes
    /// (S4.2 spike contract item); null only when the snapshot is null. Validated
    /// when present by <see cref="ReplayCodec"/> so legacy files stay readable.</summary>
    public string? InitialSnapshotHash { get; init; }

    public int EventCount => Entries.Count;

    public ReplayFile(string frameworkVersion, int dataVersion, int frameCount, IReadOnlyList<ReplayEntry> entries,
        byte[]? initialSnapshot = null, string? initialSnapshotHash = null)
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
        InitialSnapshot = initialSnapshot is null ? null : (byte[])initialSnapshot.Clone();
        InitialSnapshotHash = initialSnapshotHash;
    }

    internal static string ComputeInitialSnapshotHash(byte[] snapshotBytes)
    {
        ArgumentNullException.ThrowIfNull(snapshotBytes);
        return Convert.ToHexString(SHA256.HashData(snapshotBytes)).ToLowerInvariant();
    }
}
