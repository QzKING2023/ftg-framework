#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests.Replay;

/// <summary>
/// S4.2-A spike contract item: ReplayFile.InitialSnapshotHash — lowercase-hex
/// SHA-256 over the embedded InitialSnapshot bytes, enforced by ReplayCodec
/// validate-when-present, bound byte-exactly to the Story 2.5 save document.
/// </summary>
public sealed class InitialSnapshotHashContractTests : IDisposable
{
    private readonly string _savePath = Path.Combine(Path.GetTempPath(), $"ftg-s42a-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
    }

    private static byte[] SnapshotBytes() => StateSnapshotCodec.Encode(new StateSnapshot(1, "2.3.0", 4, 0,
        [new SnapshotComponent("input", 1, "{}")]));

    private static ReplayFile FileWithSnapshot(byte[] snapshotBytes, string? hash) =>
        new("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")], snapshotBytes, hash);

    [Fact]
    public void Ctor_WithoutSnapshotOrHash_DefaultsHashToNull()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1, []);
        Assert.Null(file.InitialSnapshot);
        Assert.Null(file.InitialSnapshotHash);
    }

    [Fact]
    public void EncodeDecode_RoundTripsInitialSnapshotHash()
    {
        byte[] snapshotBytes = SnapshotBytes();
        string hash = ReplayFile.ComputeInitialSnapshotHash(snapshotBytes);
        ReplayFile decoded = ReplayCodec.Decode(
            ReplayCodec.Encode(FileWithSnapshot(snapshotBytes, hash)), "2.3.0");
        Assert.Equal(hash, decoded.InitialSnapshotHash);
    }

    [Fact]
    public void Decode_SnapshotWithMatchingHash_Passes()
    {
        byte[] snapshotBytes = SnapshotBytes();
        ReplayFile decoded = ReplayCodec.Decode(
            ReplayCodec.Encode(FileWithSnapshot(snapshotBytes, ReplayFile.ComputeInitialSnapshotHash(snapshotBytes))),
            "2.3.0");
        Assert.Equal(snapshotBytes, decoded.InitialSnapshot);
    }

    [Fact]
    public void Decode_SnapshotWithWrongHash_IsRejected()
    {
        byte[] snapshotBytes = SnapshotBytes();
        string wrong = new('a', 64);
        Assert.NotEqual(ReplayFile.ComputeInitialSnapshotHash(snapshotBytes), wrong);
        Assert.Throws<InvalidDataException>(() =>
            ReplayCodec.Decode(ReplayCodec.Encode(FileWithSnapshot(snapshotBytes, wrong)), "2.3.0"));
    }

    [Fact]
    public void Decode_SnapshotWithoutHash_AcceptedAsLegacy()
    {
        // S4.2-C (relaxed): snapshot-bearing files recorded before the hash
        // field existed remain readable — the hash is validate-when-present.
        byte[] snapshotBytes = SnapshotBytes();
        ReplayFile decoded = ReplayCodec.Decode(
            ReplayCodec.Encode(FileWithSnapshot(snapshotBytes, null)), "2.3.0");
        Assert.Equal(snapshotBytes, decoded.InitialSnapshot);
        Assert.Null(decoded.InitialSnapshotHash);
    }

    [Fact]
    public void Decode_SnapshotWithMalformedHash_IsRejected()
    {
        string malformed = new('z', 32);
        Assert.Throws<InvalidDataException>(() =>
            ReplayCodec.Decode(ReplayCodec.Encode(FileWithSnapshot(SnapshotBytes(), malformed)), "2.3.0"));
    }

    [Fact]
    public void Decode_SnapshotWithUppercaseMatchingHash_IsAccepted()
    {
        byte[] snapshotBytes = SnapshotBytes();
        string upper = ReplayFile.ComputeInitialSnapshotHash(snapshotBytes).ToUpperInvariant();
        ReplayFile decoded = ReplayCodec.Decode(
            ReplayCodec.Encode(FileWithSnapshot(snapshotBytes, upper)), "2.3.0");
        Assert.Equal(upper, decoded.InitialSnapshotHash);
    }

    [Fact]
    public void Decode_HashWithoutInitialSnapshot_IsRejected()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")], null, new string('b', 64));
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_LegacyFileWithoutSnapshotOrHash_RemainsReadable()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")]);
        ReplayFile decoded = ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0");
        Assert.Single(decoded.Entries);
        Assert.Null(decoded.InitialSnapshot);
        Assert.Null(decoded.InitialSnapshotHash);
    }

    [Fact]
    public void ComputeInitialSnapshotHash_IsLowercaseHexSha256()
    {
        byte[] bytes = SnapshotBytes();
        string expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(expected, ReplayFile.ComputeInitialSnapshotHash(bytes));
        Assert.Equal(expected, expected.ToLowerInvariant());
    }

    [Fact]
    public void ByteExactBinding_SaveDocumentContainerHashIsReplayHash()
    {
        var snapshot = new StateSnapshot(1, "2.3.0", 7, 3,
            [new SnapshotComponent("input", 1, "{}")]);
        TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, _savePath);
        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);

        byte[] containerBytes = TrainingStatePersistence.ExtractVerifiedContainer(
            File.ReadAllBytes(_savePath), out string digest);
        Assert.Equal(TrainingStatePersistence.ComputeDigest(snapshot), digest);
        Assert.Equal(digest, ReplayFile.ComputeInitialSnapshotHash(containerBytes));

        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")], containerBytes, digest);
        ReplayFile decoded = ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0");
        Assert.Equal(containerBytes, decoded.InitialSnapshot);
        Assert.Equal(digest, decoded.InitialSnapshotHash);
    }

    [Fact]
    public void ByteExactBinding_WrongHashAgainstRealSaveDocument_IsRejected()
    {
        var snapshot = new StateSnapshot(1, "2.3.0", 7, 3,
            [new SnapshotComponent("input", 1, "{}")]);
        TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, _savePath);
        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);
        byte[] containerBytes = TrainingStatePersistence.ExtractVerifiedContainer(
            File.ReadAllBytes(_savePath), out string digest);

        string wrong = digest == new string('c', 64) ? new string('d', 64) : new string('c', 64);
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")], containerBytes, wrong);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_StateScopedWithoutSnapshot_IsRejected()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")],
            stateScoped: true);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_StateScopedWithoutHash_IsRejected()
    {
        byte[] snapshotBytes = SnapshotBytes();
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")], snapshotBytes, null, true);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_StateScopedEntryPrecedingSnapshotDomain_IsRejected()
    {
        byte[] snapshotBytes = SnapshotBytes(); // snapshot.Frame == 0 → domain starts at 1
        string hash = ReplayFile.ComputeInitialSnapshotHash(snapshotBytes);
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 2,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")], snapshotBytes, hash, true);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_StateScopedEntryWithinSnapshotDomain_IsAccepted()
    {
        byte[] snapshotBytes = SnapshotBytes(); // snapshot.Frame == 0 → domain starts at 1
        string hash = ReplayFile.ComputeInitialSnapshotHash(snapshotBytes);
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 2,
            [new ReplayEntry(1, "FrameAdvancedEvent", "{\"FrameNumber\":1}")], snapshotBytes, hash, true);
        ReplayFile decoded = ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0");
        Assert.True(decoded.StateScoped);
        Assert.Equal(1, decoded.Entries[0].Frame);
    }
}
