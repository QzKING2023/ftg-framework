#nullable enable
using System;
using System.Linq;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class TrainingInputRecordingTests
{
    [Fact]
    public void Codec_RoundTripsCanonicalRecordingDeterministically()
    {
        var recording = Recording("测试🎮", 10,
            new(0, 0, InputType.Directional, (int)DirectionValue.Neutral),
            new(10, 0, InputType.Button, (int)ButtonValue.A));

        byte[] first = TrainingInputRecordingCodec.Encode(recording);
        byte[] second = TrainingInputRecordingCodec.Encode(recording);
        var decoded = TrainingInputRecordingCodec.Decode(first);

        Assert.Equal(first, second);
        Assert.Equal(TrainingInputRecording.CurrentSchemaVersion, decoded.SchemaVersion);
        Assert.Equal(recording.SourcePlayer, decoded.SourcePlayer);
        Assert.Equal(recording.DurationFrames, decoded.DurationFrames);
        Assert.Equal(recording.Name, decoded.Name);
        Assert.Equal(recording.Entries, decoded.Entries);
    }

    [Fact]
    public void FinalizedRecording_DeepCopiesEntrySource()
    {
        var entries = new[] { new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.A) };
        var recording = new TrainingInputRecording(1, 1, 0, "one", entries);

        entries[0] = new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.D);

        Assert.Equal((int)ButtonValue.A, recording.Entries[0].InputValue);
        Assert.False(recording.Entries is TrainingInputRecordingEntry[]);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 36001, 0)]
    public void Constructor_RejectsInvalidHeader(int player, int duration, int entryCount)
    {
        var entries = Enumerable.Range(0, entryCount)
            .Select(i => new TrainingInputRecordingEntry(i, 0, InputType.Directional, 5)).ToArray();
        Assert.Throws<TrainingInputRecordingException>(() =>
            new TrainingInputRecording(1, player, duration, "bad", entries));
    }

    [Fact]
    public void Constructor_RejectsInvalidOrderAndIdentity()
    {
        AssertInvalid(new TrainingInputRecordingEntry(0, 1, InputType.Directional, 5));
        AssertInvalid(new TrainingInputRecordingEntry(-1, 0, InputType.Directional, 5));
        AssertInvalid(new TrainingInputRecordingEntry(1, 0, InputType.Directional, 5), new TrainingInputRecordingEntry(0, 0, InputType.Directional, 5));
        AssertInvalid(new TrainingInputRecordingEntry(0, 0, InputType.Directional, 5), new TrainingInputRecordingEntry(0, 2, InputType.Button, 0));
        AssertInvalid(new TrainingInputRecordingEntry(0, 0, (InputType)99, 0));
        AssertInvalid(new TrainingInputRecordingEntry(0, 0, InputType.Directional, 99));
        AssertInvalid(new TrainingInputRecordingEntry(0, 0, InputType.Button, 99));

        static void AssertInvalid(params TrainingInputRecordingEntry[] entries) =>
            Assert.Throws<TrainingInputRecordingException>(() =>
                new TrainingInputRecording(1, 1, 1, "bad", entries));
    }

    [Fact]
    public void Constructor_EnforcesUnicodeScalarNameLimit()
    {
        string max = string.Concat(Enumerable.Repeat("🎮", 128));
        _ = Recording(max, 0);
        Assert.Throws<TrainingInputRecordingException>(() => Recording(max + "x", 0));
    }

    [Fact]
    public void Constructor_EnforcesEntryLimit()
    {
        var max = Enumerable.Range(0, TrainingInputRecording.MaxEntries)
            .Select(i => new TrainingInputRecordingEntry(0, i, InputType.Directional, 5)).ToArray();
        _ = new TrainingInputRecording(1, 1, TrainingInputRecording.MaxDurationFrames, "max", max);
        var tooMany = max.Append(new TrainingInputRecordingEntry(
            0, TrainingInputRecording.MaxEntries, InputType.Button, 0)).ToArray();
        Assert.Throws<TrainingInputRecordingException>(() =>
            new TrainingInputRecording(1, 1, TrainingInputRecording.MaxDurationFrames, "bad", tooMany));
    }

    [Fact]
    public void Decode_RejectsMalformedUnknownVersionAndOversizedPayload()
    {
        Assert.Throws<TrainingInputRecordingException>(() =>
            TrainingInputRecordingCodec.Decode(Encoding.UTF8.GetBytes("{")));
        Assert.Throws<TrainingInputRecordingException>(() =>
            TrainingInputRecordingCodec.Decode(Encoding.UTF8.GetBytes("{\"schema_version\":2}")));
        Assert.Throws<TrainingInputRecordingException>(() =>
            TrainingInputRecordingCodec.Decode(new byte[TrainingInputRecording.MaxEncodedBytes + 1]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"name\":123")]
    public void Decode_RejectsMissingOrNonStringName(string nameProperty)
    {
        string payload =
            $"{{\"schema_version\":1,\"source_player\":1,\"duration_frames\":0{nameProperty},\"entries\":[]}}";

        Assert.Throws<TrainingInputRecordingException>(() =>
            TrainingInputRecordingCodec.Decode(Encoding.UTF8.GetBytes(payload)));
    }

    [Fact]
    public void Library_RejectsCandidateWithoutReplacingAssignedRecording()
    {
        var library = new TrainingInputRecordingLibrary();
        var accepted = Recording("accepted", 0);
        Assert.True(library.TryAddOrReplace(accepted, confirmReplace: false, null, out _));
        Assert.True(library.TryAssign(2, "accepted", out _));

        Assert.False(library.TryDecodeAndAddOrReplace(
            Encoding.UTF8.GetBytes("{\"schema_version\":99}"), false, null, out _));

        Assert.Same(accepted, library.GetAssigned(2));
        Assert.Single(library.Recordings);
    }

    [Fact]
    public void Library_ConfirmedReplacementRebindsAssignmentsAtomically()
    {
        var library = new TrainingInputRecordingLibrary();
        var oldRecording = Recording("setup", 1,
            new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.A));
        var newRecording = Recording("setup", 2,
            new TrainingInputRecordingEntry(0, 0, InputType.Button, (int)ButtonValue.B));
        Assert.True(library.TryAddOrReplace(oldRecording, false, null, out _));
        Assert.True(library.TryAssign(1, "setup", out _));
        Assert.True(library.TryAssign(2, "setup", out _));

        Assert.True(library.TryAddOrReplace(newRecording, true, activePlayback: null, out _));

        Assert.Same(newRecording, library.GetAssigned(1));
        Assert.Same(newRecording, library.GetAssigned(2));
        Assert.Same(newRecording, library.Recordings["setup"]);
        Assert.Single(library.Recordings);
    }

    [Fact]
    public void Library_ActiveTargetAndPrecommitFailurePreserveOldState()
    {
        var library = new TrainingInputRecordingLibrary();
        var oldRecording = Recording("setup", 1);
        var candidate = Recording("setup", 2);
        Assert.True(library.TryAddOrReplace(oldRecording, false, null, out _));
        Assert.True(library.TryAssign(2, "setup", out _));

        Assert.False(library.TryAddOrReplace(candidate, true, oldRecording, out string activeError));
        Assert.Contains("stop playback", activeError, StringComparison.OrdinalIgnoreCase);
        Assert.False(library.TryAddOrReplace(candidate, true, null, out _, () => true));

        Assert.Same(oldRecording, library.Recordings["setup"]);
        Assert.Same(oldRecording, library.GetAssigned(2));
    }

    [Fact]
    public void Library_OverwriteUsesOrdinalNamesAndPreservesUnrelatedActiveRecording()
    {
        var library = new TrainingInputRecordingLibrary();
        var lower = Recording("setup", 1);
        var upper = Recording("Setup", 2);
        var replacement = Recording("setup", 3);
        Assert.True(library.TryAddOrReplace(lower, false, null, out _));
        Assert.True(library.TryAddOrReplace(upper, false, null, out _));
        Assert.True(library.TryAssign(1, "Setup", out _));

        Assert.False(library.TryAddOrReplace(replacement, false, upper, out _));
        Assert.Same(lower, library.Recordings["setup"]);
        Assert.True(library.TryAddOrReplace(replacement, true, upper, out _));

        Assert.Equal(2, library.Recordings.Count);
        Assert.Same(upper, library.GetAssigned(1));
        Assert.Equal(3, TrainingInputRecordingCodec.Decode(
            TrainingInputRecordingCodec.Encode(library.Recordings["setup"])).DurationFrames);
    }

    private static TrainingInputRecording Recording(string name, int duration,
        params TrainingInputRecordingEntry[] entries) =>
        new(TrainingInputRecording.CurrentSchemaVersion, 1, duration, name, entries);
}
