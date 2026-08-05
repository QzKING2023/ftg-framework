#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using FTG_Framework.Core;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class TrainingStatePersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-training-save-{Guid.NewGuid():N}");

    public TrainingStatePersistenceTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Save_ProducesReadableCanonicalFileWithIntegrityMetadata()
    {
        string path = Path.Combine(_directory, "save-a.json");
        StateSnapshot snapshot = Snapshot("state_machine", "{\"value\":1}");

        TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, path);

        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Sha256));
        string text = File.ReadAllText(path);
        Assert.Contains("\"save_schema_version\":1", text, StringComparison.Ordinal);
        Assert.Contains("\"schema_version\":1", text, StringComparison.Ordinal);
        Assert.Contains("\"framework_version\":\"2.3.0\"", text, StringComparison.Ordinal);
        Assert.Contains("\"source_epoch\":", text, StringComparison.Ordinal);
        Assert.Contains("\"frame\":12", text, StringComparison.Ordinal);
        Assert.Contains("\"discriminator\":\"state_machine\"", text, StringComparison.Ordinal);
        Assert.Contains("\"algorithm\":\"sha256\"", text, StringComparison.Ordinal);
        Assert.Contains("\"input_encoding\":\"utf8\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"digest\":\"{result.Sha256}\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsContainerByteForByte()
    {
        string path = Path.Combine(_directory, "roundtrip.json");
        StateSnapshot original = Snapshot("state_machine", "{\"stacks\":{}}",
            ("physics_motion", "{\"generations\":{}}"));

        TrainingSaveResult result = TrainingStatePersistence.Save(original, path);
        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);
        StateSnapshot loaded = TrainingStatePersistence.Load(path, out string digest);

        Assert.Equal(original.Frame, loaded.Frame);
        Assert.Equal(original.SourceEpoch, loaded.SourceEpoch);
        Assert.Equal(original.FrameworkVersion, loaded.FrameworkVersion);
        Assert.Equal(original.Components.Count, loaded.Components.Count);
        Assert.Equal(result.Sha256, digest);
        Assert.Equal(2, loaded.Components.Count);
    }

    [Fact]
    public void Save_ExceedingMaxFileSize_FailsWithoutWriting()
    {
        string path = Path.Combine(_directory, "oversize.json");
        string bigPayload = new string('x', TrainingStatePersistence.MaxComponentPayloadBytes + 1);

        TrainingSaveResult result = TrainingStatePersistence.Save(
            Snapshot("state_machine", bigPayload), path);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("P-SAVE", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Save_ComponentCountOverLimit_FailsWithoutWriting()
    {
        string path = Path.Combine(_directory, "many.json");
        var components = new List<SnapshotComponent>();
        for (int i = 0; i < TrainingStatePersistence.MaxComponentRecords + 1; i++)
            components.Add(new SnapshotComponent($"comp_{i:D3}", 1, "{}"));

        TrainingSaveResult result = TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 1, components), path);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("64", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Save_ExactComponentLimit_Succeeds()
    {
        string path = Path.Combine(_directory, "exact-many.json");
        var components = new List<SnapshotComponent>();
        for (int i = 0; i < TrainingStatePersistence.MaxComponentRecords; i++)
            components.Add(new SnapshotComponent($"comp_{i:D3}", 1, "{}"));

        TrainingSaveResult result = TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 1, components), path);

        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_ExactPayloadBoundary_Succeeds()
    {
        string path = Path.Combine(_directory, "exact-payload.json");
        string payload = new string('x', TrainingStatePersistence.MaxComponentPayloadBytes);

        TrainingSaveResult result = TrainingStatePersistence.Save(
            Snapshot("state_machine", payload), path);

        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_TotalFileLimit_TriggersOnContainerSizeNotPayloadSize()
    {
        string path = Path.Combine(_directory, "container-oversize.json");
        // Five payloads at the per-component limit fit individually but push the
        // total container past the 16 MiB file limit.
        string payload = new string('x', TrainingStatePersistence.MaxComponentPayloadBytes);
        var components = new List<SnapshotComponent>();
        for (int i = 0; i < 5; i++)
            components.Add(new SnapshotComponent($"comp_{i}", 1, payload));

        TrainingSaveResult result = TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 1, components), path);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("16", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Save_WrapperBandOverFileLimit_RejectsWithoutWriting()
    {
        // Container just under the file limit but the wrapper pushes the complete
        // save document over it — the save must fail instead of writing a file
        // that Load would permanently reject. The payload size is calibrated
        // against the actual container overhead so the container lands inside
        // the (limit - wrapper, limit] band.
        string path = Path.Combine(_directory, "wrapper-band.json");
        var components = new List<SnapshotComponent>();
        for (int i = 0; i < 4; i++)
            components.Add(new SnapshotComponent($"comp_{i}", 1, new string('x', 4_194_200)));
        var probe = new StateSnapshot(1, "2.3.0", 1, 1, components);
        int overhead = StateSnapshotCodec.Encode(probe).Length - 4 * 4_194_200;
        int payloadSize = (TrainingStatePersistence.MaxFileBytes - 20 - overhead) / 4;
        components.Clear();
        for (int i = 0; i < 4; i++)
            components.Add(new SnapshotComponent($"comp_{i}", 1, new string('x', payloadSize)));
        int containerBytes = StateSnapshotCodec.Encode(new StateSnapshot(1, "2.3.0", 1, 1, components)).Length;
        Assert.InRange(containerBytes, TrainingStatePersistence.MaxFileBytes - 300, TrainingStatePersistence.MaxFileBytes);

        TrainingSaveResult result = TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 1, components), path);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("Save document is", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Save_DiscriminatorLengthBoundary_ExactOneTwentyEightSucceedsOverRejects()
    {
        string boundary = new string('a', TrainingStatePersistence.MaxDiscriminatorLength);
        string over = new string('a', TrainingStatePersistence.MaxDiscriminatorLength + 1);
        string okPath = Path.Combine(_directory, "boundary-ok.json");
        string failPath = Path.Combine(_directory, "boundary-over.json");

        Assert.Equal(TrainingSaveStatus.Succeeded,
            TrainingStatePersistence.Save(Snapshot(boundary, "{}"), okPath).Status);
        TrainingSaveResult overResult = TrainingStatePersistence.Save(Snapshot(over, "{}"), failPath);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, overResult.Status);
        Assert.Contains("ASCII", overResult.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(failPath));
    }

    [Fact]
    public void Save_NonAsciiDiscriminator_FailsWithoutWriting()
    {
        string path = Path.Combine(_directory, "bad-id.json");

        TrainingSaveResult result = TrainingStatePersistence.Save(
            Snapshot("状态机", "{}"), path);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("ASCII", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Save_FailureAtEverySeam_PreservesPriorFileByteForByte()
    {
        string path = Path.Combine(_directory, "atomic.json");
        StateSnapshot snapshot = Snapshot("state_machine", "{\"value\":1}");
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(snapshot, path).Status);
        byte[] prior = File.ReadAllBytes(path);

        foreach (TrainingPersistenceFaultPoint point in Enum.GetValues<TrainingPersistenceFaultPoint>())
        {
            if (point is TrainingPersistenceFaultPoint.DuringPostCommitCleanup or
                TrainingPersistenceFaultPoint.DuringErrorCleanup)
                continue;
            TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, path,
                fault: injected => { if (injected == point) throw new IOException($"injected:{point}"); });
            Assert.Equal(TrainingSaveStatus.Failed, result.Status);
            Assert.Equal(prior, File.ReadAllBytes(path));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }
    }

    [Fact]
    public void Save_ErrorCleanupFault_IsRecordedDuringFailurePath()
    {
        string path = Path.Combine(_directory, "error-cleanup.json");
        StateSnapshot snapshot = Snapshot("state_machine", "{\"value\":1}");
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(snapshot, path).Status);
        byte[] prior = File.ReadAllBytes(path);

        TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, path,
            fault: injected =>
            {
                if (injected == TrainingPersistenceFaultPoint.BeforeReplace) throw new IOException("injected:replace");
                if (injected == TrainingPersistenceFaultPoint.DuringErrorCleanup) throw new IOException("injected:cleanup");
            });

        Assert.Equal(TrainingSaveStatus.Failed, result.Status);
        Assert.Equal(prior, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Save_PostCommitCleanupFault_IsRecordedWithoutLosingCommit()
    {
        string path = Path.Combine(_directory, "post-commit.json");
        StateSnapshot snapshot = Snapshot("state_machine", "{\"value\":1}");

        TrainingSaveResult result = TrainingStatePersistence.Save(snapshot, path,
            fault: injected =>
            {
                if (injected == TrainingPersistenceFaultPoint.DuringPostCommitCleanup)
                    throw new IOException("injected:post-commit");
            });

        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);
        Assert.NotNull(result.Sha256);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Load_ManualEditFailsIntegrityCheck()
    {
        string path = Path.Combine(_directory, "edited.json");
        StateSnapshot snapshot = Snapshot("state_machine", "{\"value\":1}");
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(snapshot, path).Status);
        string text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"frame\":12", "\"frame\":13", StringComparison.Ordinal));

        var ex = Assert.Throws<TrainingStateLoadException>(() => TrainingStatePersistence.Load(path, out _));
        Assert.Contains("integrity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UnsupportedSchemaVersion_Rejects()
    {
        string path = Path.Combine(_directory, "schema.json");
        StateSnapshot snapshot = Snapshot("state_machine", "{}");
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(snapshot, path).Status);
        string text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"save_schema_version\":1", "\"save_schema_version\":2", StringComparison.Ordinal));

        var ex = Assert.Throws<TrainingStateLoadException>(() => TrainingStatePersistence.Load(path, out _));
        Assert.Contains("schema", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingFile_ReturnsActionableError()
    {
        string path = Path.Combine(_directory, "missing.json");
        var ex = Assert.Throws<TrainingStateLoadException>(() => TrainingStatePersistence.Load(path, out _));
        Assert.Contains("Cannot", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FileOverMaxBytes_RejectsBeforeAllocation()
    {
        string path = Path.Combine(_directory, "huge.json");
        using (var stream = File.Create(path))
        {
            stream.SetLength(TrainingStatePersistence.MaxFileBytes + 1);
        }
        var ex = Assert.Throws<TrainingStateLoadException>(() => TrainingStatePersistence.Load(path, out _));
        Assert.Contains("limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MalformedJson_RejectsWithActionableError()
    {
        string path = Path.Combine(_directory, "garbage.json");
        File.WriteAllText(path, "{not json");

        var ex = Assert.Throws<TrainingStateLoadException>(() => TrainingStatePersistence.Load(path, out _));
        Assert.Contains("JSON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_RejectsContainerPayloadOverFourMiB_PreservingPriorFile()
    {
        string path = Path.Combine(_directory, "payload.json");
        StateSnapshot prior = Snapshot("state_machine", "{\"value\":\"old\"}");
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(prior, path).Status);
        byte[] before = File.ReadAllBytes(path);

        TrainingSaveResult result = TrainingStatePersistence.Save(
            Snapshot("state_machine", new string('y', TrainingStatePersistence.MaxComponentPayloadBytes + 1)), path);

        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_OverwriteRoundTrip_ReplacesPriorSave()
    {
        string path = Path.Combine(_directory, "replace.json");
        Assert.Equal(TrainingSaveStatus.Succeeded,
            TrainingStatePersistence.Save(Snapshot("state_machine", "{\"v\":1}"), path).Status);
        string sha1 = File.ReadAllText(path);

        Assert.Equal(TrainingSaveStatus.Succeeded,
            TrainingStatePersistence.Save(Snapshot("state_machine", "{\"v\":2}"), path).Status);

        string sha2 = File.ReadAllText(path);
        Assert.NotEqual(sha1, sha2);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Single(Directory.GetFiles(_directory));
    }

    private static StateSnapshot Snapshot(string discriminator, string payload,
        params (string Discriminator, string Payload)[] additional)
    {
        var components = new List<SnapshotComponent> { new(discriminator, 1, payload) };
        foreach ((string id, string body) in additional)
            components.Add(new SnapshotComponent(id, 1, body));
        return new StateSnapshot(1, "2.3.0", 42, 12, components);
    }
}
