#nullable enable
using System;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public sealed class ReplayCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTripsCanonicalContainerAndIntegrityHash()
    {
        byte[] initialSnapshot = StateSnapshotCodec.Encode(new StateSnapshot(1, "2.3.0", 4, 0,
            [new SnapshotComponent("input", 1, "{}")]));
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 2,
        [
            new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}", phase: 1, sequence: 0, sourceEpoch: 4),
            new ReplayEntry(1, "InputReceivedEvent", "{\"PlayerId\":1,\"InputType\":0,\"Value\":1}", phase: 2, sequence: 0, sourceEpoch: 4)
        ], initialSnapshot);

        byte[] encoded = ReplayCodec.Encode(file);
        ReplayFile decoded = ReplayCodec.Decode(encoded, "2.3.0");

        Assert.Equal(file.FrameCount, decoded.FrameCount);
        Assert.Equal(file.EventCount, decoded.EventCount);
        Assert.Equal(1, decoded.Entries[1].Frame);
        Assert.Equal(2, decoded.Entries[1].Phase);
        Assert.Equal(0, decoded.Entries[1].Sequence);
        Assert.Equal((ulong)4, decoded.Entries[1].SourceEpoch);
        Assert.Equal(initialSnapshot, decoded.InitialSnapshot);
    }

    [Fact]
    public void Decode_TamperedPayload_IsRejectedAtomically()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}", 1, 0, 1)]);
        byte[] encoded = ReplayCodec.Encode(file);
        int index = Array.IndexOf(encoded, (byte)'0');
        Assert.True(index >= 0);
        encoded[index] = (byte)'9';

        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(encoded, "2.3.0"));
    }

    [Fact]
    public void Decode_UnknownEvent_IsRejectedBeforePlaybackMutation()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "UnknownEvent", "{}", 7, 0, 1)]);

        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_LegacyV3DirectJson_RemainsReadable()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "FrameAdvancedEvent", "{\"FrameNumber\":0}")]);
        byte[] legacy = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(file);
        ReplayFile decoded = ReplayCodec.Decode(legacy, "2.3.0");
        Assert.Single(decoded.Entries);
    }

    [Fact]
    public void Decode_MissingContainerFields_IsControlledInvalidData()
    {
        byte[] malformed = System.Text.Encoding.UTF8.GetBytes(
            "{\"ContainerVersion\":1,\"IntegrityHash\":null,\"Payload\":null}");
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(malformed, "2.3.0"));
    }

    [Fact]
    public void Decode_EventOutsideFrameCount_IsRejected()
    {
        var file = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(1, "FrameAdvancedEvent", "{\"FrameNumber\":1}", 1, 0, 1)]);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(file), "2.3.0"));
    }

    [Fact]
    public void Decode_DuplicateOrGappedWithinPhaseSequence_IsRejected()
    {
        var duplicate = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
        [
            new ReplayEntry(0, "InputReceivedEvent", "{\"PlayerId\":1,\"InputType\":0,\"Value\":1}", 2, 0, 1),
            new ReplayEntry(0, "InputReceivedEvent", "{\"PlayerId\":2,\"InputType\":0,\"Value\":1}", 2, 0, 1)
        ]);
        var gap = new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1,
            [new ReplayEntry(0, "InputReceivedEvent", "{\"PlayerId\":1,\"InputType\":0,\"Value\":1}", 2, 1, 1)]);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(duplicate), "2.3.0"));
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Decode(ReplayCodec.Encode(gap), "2.3.0"));
    }
}
