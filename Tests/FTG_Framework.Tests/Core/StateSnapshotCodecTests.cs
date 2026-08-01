#nullable enable
using System;
using System.IO;
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests.Core;

public sealed class StateSnapshotCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTripsValueOnlyContainerInCanonicalOrder()
    {
        var snapshot = new StateSnapshot(1, "2.3.0", 7, 12,
        [
            new SnapshotComponent("state_machine", 2, "{\"players\":[1,2]}"),
            new SnapshotComponent("frame_data", 1, "{\"frame\":12}")
        ]);

        byte[] first = StateSnapshotCodec.Encode(snapshot);
        byte[] second = StateSnapshotCodec.Encode(snapshot);
        StateSnapshot decoded = StateSnapshotCodec.Decode(first);

        Assert.Equal(first, second);
        Assert.Equal(["frame_data", "state_machine"], decoded.Components.Select(c => c.Discriminator));
        Assert.Equal((ulong)7, decoded.SourceEpoch);
        Assert.Equal(12, decoded.Frame);
    }

    [Fact]
    public void Decode_DuplicateDiscriminator_IsRejected()
    {
        const string json = """
            {"schema_version":1,"framework_version":"2.3.0","source_epoch":1,"frame":0,"components":[{"discriminator":"input","codec_version":1,"payload":"{}"},{"discriminator":"input","codec_version":1,"payload":"{}"}]}
            """;
        Assert.Throws<InvalidDataException>(() => StateSnapshotCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Constructor_DefensivelyCopiesComponents()
    {
        SnapshotComponent[] components = [new("input", 1, "{}")];
        var snapshot = new StateSnapshot(1, "2.3.0", 1, 0, components);
        components[0] = new SnapshotComponent("mutated", 1, "{}");
        Assert.Equal("input", snapshot.Components[0].Discriminator);
    }

    [Fact]
    public void Decode_NullComponent_IsControlledInvalidData()
    {
        const string json = """
            {"schema_version":1,"framework_version":"2.3.0","source_epoch":1,"frame":0,"components":[null]}
            """;
        Assert.Throws<InvalidDataException>(() => StateSnapshotCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json)));
    }
}
