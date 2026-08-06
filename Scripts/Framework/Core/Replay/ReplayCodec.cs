#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace FTG_Framework.Core.Replay;

/// <summary>Sole UTF-8 persistence boundary for replay containers.</summary>
public static class ReplayCodec
{
    private const int ContainerVersion = 1;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    public static byte[] Encode(ReplayFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.InitialSnapshot is not null)
            _ = StateSnapshotCodec.Decode(file.InitialSnapshot);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(file, Options);
        string hash = Convert.ToHexString(SHA256.HashData(payload));
        return JsonSerializer.SerializeToUtf8Bytes(new ReplayContainer(ContainerVersion, hash, payload), Options);
    }

    public static ReplayFile Decode(ReadOnlySpan<byte> utf8, string currentFrameworkVersion)
    {
        try
        {
            using JsonDocument probe = JsonDocument.Parse(utf8.ToArray());
            if (!probe.RootElement.TryGetProperty(nameof(ReplayContainer.ContainerVersion), out _))
                return Validate(JsonSerializer.Deserialize<ReplayFile>(utf8, Options)
                    ?? throw new InvalidDataException("[Replay] Legacy payload is empty."), currentFrameworkVersion);
        }
        catch (JsonException ex) { throw new InvalidDataException("[Replay] Invalid UTF-8 JSON container.", ex); }

        ReplayContainer container;
        try
        {
            container = JsonSerializer.Deserialize<ReplayContainer>(utf8, Options)
                ?? throw new InvalidDataException("[Replay] Container is empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("[Replay] Invalid UTF-8 JSON container.", ex); }
        if (container.ContainerVersion != ContainerVersion)
            throw new InvalidDataException($"[Replay] Unsupported container version {container.ContainerVersion}.");
        if (container.Payload is null || string.IsNullOrWhiteSpace(container.IntegrityHash))
            throw new InvalidDataException("[Replay] Container payload and integrity hash are required.");
        string actual = Convert.ToHexString(SHA256.HashData(container.Payload));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(container.IntegrityHash)))
            throw new InvalidDataException("[Replay] Integrity hash mismatch.");

        ReplayFile file;
        try
        {
            file = JsonSerializer.Deserialize<ReplayFile>(container.Payload, Options)
                ?? throw new InvalidDataException("[Replay] Payload is empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("[Replay] Invalid replay payload.", ex); }
        return Validate(file, currentFrameworkVersion);
    }

    private static ReplayFile Validate(ReplayFile file, string currentFrameworkVersion)
    {
        ReplayVersionValidator.ValidateVersion(file.DataVersion);
        ReplayVersionValidator.ValidateFrameworkVersion(file.FrameworkVersion, currentFrameworkVersion);
        StateSnapshot? initialSnapshot = null;
        if (file.InitialSnapshot is not null)
        {
            initialSnapshot = StateSnapshotCodec.Decode(file.InitialSnapshot);
            // S4.2 spike contract (validate-when-present, relaxed for legacy):
            // the declared initial-snapshot hash is enforced when present —
            // files recorded before the field existed remain readable.
            if (file.InitialSnapshotHash is not null &&
                (!IsHexDigest(file.InitialSnapshotHash) ||
                 !string.Equals(ReplayFile.ComputeInitialSnapshotHash(file.InitialSnapshot), file.InitialSnapshotHash,
                     StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("[Replay] InitialSnapshotHash does not match the embedded initial snapshot.");
        }
        else if (file.InitialSnapshotHash is not null)
        {
            throw new InvalidDataException("[Replay] InitialSnapshotHash is present without an initial snapshot.");
        }
        // S4.2-C format marker: state-scoped files are recorded from a Story 2.5
        // training snapshot and must carry the snapshot plus its stamped hash,
        // with an absolute frame domain starting at snapshot.frame + 1.
        // Ordinary recordings and legacy files start at frame 0.
        if (file.StateScoped)
        {
            if (initialSnapshot is null)
                throw new InvalidDataException("[Replay] State-scoped replay requires an embedded initial snapshot.");
            if (string.IsNullOrWhiteSpace(file.InitialSnapshotHash))
                throw new InvalidDataException("[Replay] State-scoped replay requires an initial snapshot hash.");
            if (file.FrameCount <= initialSnapshot.Frame)
                throw new InvalidDataException(
                    $"[Replay] State-scoped FrameCount {file.FrameCount} must exceed snapshot frame {initialSnapshot.Frame}.");
        }
        if (file.Entries is null)
            throw new InvalidDataException("[Replay] Entries are required.");
        var sequences = new Dictionary<(int Frame, int Phase), HashSet<int>>();
        foreach (ReplayEntry? candidate in file.Entries)
        {
            if (candidate is null)
                throw new InvalidDataException("[Replay] Entries cannot contain null.");
            ReplayEntry entry = candidate;
            if (entry.Frame >= file.FrameCount)
                throw new InvalidDataException($"[Replay] Event frame {entry.Frame} is outside FrameCount {file.FrameCount}.");
            if (file.StateScoped && entry.Frame < initialSnapshot!.Frame + 1)
                throw new InvalidDataException(
                    $"[Replay] Event frame {entry.Frame} precedes the state-scoped domain starting at {initialSnapshot.Frame + 1}.");
            Type? eventType = EventTypeRegistry.Resolve(entry.EventType);
            if (eventType is null)
                throw new InvalidDataException($"[Replay] Unknown event discriminator '{entry.EventType}'.");
            int expectedPhase = EventTypeRegistry.GetPhase(eventType);
            int effectivePhase = entry.Phase == 0 ? expectedPhase : entry.Phase;
            if (effectivePhase != expectedPhase)
                throw new InvalidDataException($"[Replay] Event '{entry.EventType}' has phase {entry.Phase}; expected {expectedPhase}.");
            if (entry.Phase != 0)
            {
                var sequenceKey = (entry.Frame, effectivePhase);
                if (!sequences.TryGetValue(sequenceKey, out HashSet<int>? phaseSequences))
                    sequences[sequenceKey] = phaseSequences = new HashSet<int>();
                if (!phaseSequences.Add(entry.Sequence))
                    throw new InvalidDataException($"[Replay] Duplicate sequence {entry.Sequence} in frame {entry.Frame}, phase {effectivePhase}.");
            }
            ReplayVersionValidator.ValidateEntryPayload(file.DataVersion, entry);
        }
        foreach (var pair in sequences)
            for (int expected = 0; expected < pair.Value.Count; expected++)
                if (!pair.Value.Contains(expected))
                    throw new InvalidDataException($"[Replay] Frame {pair.Key.Frame}, phase {pair.Key.Phase} has a non-contiguous sequence catalog.");
        return file;
    }

    public static void Write(string path, ReplayFile file) => File.WriteAllBytes(path, Encode(file));
    public static ReplayFile Read(string path, string currentFrameworkVersion) =>
        Decode(File.ReadAllBytes(path), currentFrameworkVersion);

    private static bool IsHexDigest(string value)
    {
        if (value.Length != 64) return false;
        foreach (char character in value)
            if (!Uri.IsHexDigit(character)) return false;
        return true;
    }

    private sealed record ReplayContainer(int ContainerVersion, string IntegrityHash, byte[] Payload);
}
