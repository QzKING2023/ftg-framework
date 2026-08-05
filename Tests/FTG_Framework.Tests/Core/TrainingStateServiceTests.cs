#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Data;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Core;

[Collection(EventBusTestCollection.Name)]
public sealed class TrainingStateServiceTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-training-svc-{Guid.NewGuid():N}");

    public TrainingStateServiceTests() => Directory.CreateDirectory(_directory);
    public void Dispose()
    {
        _eventBusScope.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private (TrainingStateService Service, FakeParticipant[] Participants, int Frame) Runtime(int currentFrame = 20)
    {
        FakeParticipant[] participants = SnapshotParticipantCatalog.Required
            .Select(id => new FakeParticipant(id, $"live-{id}"))
            .ToArray();
        var coordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, participants);
        var service = new TrainingStateService(coordinator, () => currentFrame, () => EventBus.Instance.LifecycleEpoch);
        return (service, participants, currentFrame);
    }

    [Fact]
    public void TrySave_CapturesLastCompletedFrame_AndRestoresAtomically()
    {
        (TrainingStateService service, FakeParticipant[] participants, int frame) = Runtime(20);
        string path = Path.Combine(_directory, "slot.json");

        Assert.True(service.TrySave(path, out string error), error);
        Assert.True(service.TryInspect(path, out var info, out error), error);
        Assert.Equal(19, info!.Frame);
        Assert.Equal(5, info.ComponentCount);
        Assert.Equal(service.LastSaveSha256, info.Sha256);

        foreach (FakeParticipant participant in participants)
            participant.LiveValue = $"mutated-{participant.Discriminator}";

        Assert.True(service.TryRestore(path, out error), error);
        foreach (FakeParticipant participant in participants)
            Assert.Equal($"live-{participant.Discriminator}", participant.LiveValue);
        Assert.Equal(frame, EventBus.Instance.CurrentFrame);
    }

    [Fact]
    public void TrySave_ExceedingLimits_ReportsActionableErrorAndWritesNothing()
    {
        (TrainingStateService service, _, _) = Runtime();
        string path = Path.Combine(_directory, "oversize.json");
        var oversized = new SnapshotComponent(
            SnapshotParticipantCatalog.StateMachine, 1, new string('x', TrainingStatePersistence.MaxComponentPayloadBytes + 1));

        // The coordinator serializes the fake payload as-is; emulate an oversized component
        // through a direct persistence call instead (service is not the limit authority).
        TrainingSaveResult result = TrainingStatePersistence.Save(
            new StateSnapshot(1, TrainingStateService.FrameworkVersion, 1, 1,
                new[] { oversized }), path);
        Assert.Equal(TrainingSaveStatus.ValidationFailed, result.Status);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TryRestore_MissingRequiredComponent_RejectsBeforeMutation()
    {
        (TrainingStateService service, FakeParticipant[] participants, _) = Runtime();
        string path = Path.Combine(_directory, "missing.json");
        SnapshotComponent[] components = participants
            .Where(p => p.Discriminator != SnapshotParticipantCatalog.StateMachine)
            .Select(p => p.Capture(0, 1))
            .ToArray();
        TrainingSaveResult result = TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 5, components), path);
        Assert.Equal(TrainingSaveStatus.Succeeded, result.Status);

        Assert.False(service.TryRestore(path, out string error));
        Assert.Contains("missing", error, StringComparison.OrdinalIgnoreCase);
        foreach (FakeParticipant participant in participants)
            Assert.Equal($"live-{participant.Discriminator}", participant.LiveValue);
    }

    [Fact]
    public void TryRestore_UnknownComponent_RejectsBeforeMutation()
    {
        (TrainingStateService service, FakeParticipant[] participants, _) = Runtime();
        string path = Path.Combine(_directory, "unknown.json");
        var components = participants
            .Select(p => p.Capture(0, 1))
            .Append(new SnapshotComponent("mystery", 1, "{}"))
            .ToArray();
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 5, components), path).Status);

        Assert.False(service.TryRestore(path, out string error));
        Assert.Contains("unknown", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryRestore_IncompatibleFramework_RejectsWithMigrationPolicy()
    {
        (TrainingStateService service, FakeParticipant[] participants, _) = Runtime();
        string path = Path.Combine(_directory, "version.json");
        var components = participants
            .Select(p => p.Capture(0, 1))
            .ToArray();
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(
            new StateSnapshot(1, "9.9.9", 1, 5, components), path).Status);

        Assert.False(service.TryRestore(path, out string error));
        Assert.Contains("migration", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryRestore_CorruptedFile_RejectsWithIntegrityError()
    {
        (TrainingStateService service, _, _) = Runtime();
        string path = Path.Combine(_directory, "corrupt.json");
        Assert.True(service.TrySave(path, out _));
        string text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"frame\":19", "\"frame\":20", StringComparison.Ordinal));

        Assert.False(service.TryRestore(path, out string error));
        Assert.Contains("integrity", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryRestore_ParticipantPrepareFailure_PreservesLiveState()
    {
        FakeParticipant[] participants = SnapshotParticipantCatalog.Required
            .Select(id => new FakeParticipant(id, $"live-{id}"))
            .ToArray();
        var coordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, participants);
        var service = new TrainingStateService(coordinator, () => 20, () => EventBus.Instance.LifecycleEpoch);
        string path = Path.Combine(_directory, "fails.json");
        Assert.True(service.TrySave(path, out _));
        ulong epoch = EventBus.Instance.LifecycleEpoch;
        string[] before = participants.Select(p => p.LiveValue).ToArray();
        participants[0].FailPrepare = true;

        Assert.False(service.TryRestore(path, out string error));
        Assert.Contains("[Snapshot]", error, StringComparison.Ordinal);
        Assert.Equal(before, participants.Select(p => p.LiveValue));
        Assert.Equal(epoch, EventBus.Instance.LifecycleEpoch);
    }

    [Fact]
    public void TryRestore_UnsupportedComponentCodecVersion_RejectsBeforeMutation()
    {
        (TrainingStateService service, FakeParticipant[] participants, _) = Runtime();
        string path = Path.Combine(_directory, "codec.json");
        SnapshotComponent[] components = participants.Select(p => p.Capture(0, 1)).ToArray();
        components[0] = new SnapshotComponent(components[0].Discriminator, 2, components[0].Payload);
        Assert.Equal(TrainingSaveStatus.Succeeded, TrainingStatePersistence.Save(
            new StateSnapshot(1, "2.3.0", 1, 5, components), path).Status);

        Assert.False(service.TryRestore(path, out string error));
        Assert.Contains("codec version", error, StringComparison.OrdinalIgnoreCase);
        foreach (FakeParticipant participant in participants)
            Assert.Equal($"live-{participant.Discriminator}", participant.LiveValue);
    }

    [Fact]
    public void InputParticipant_MissingRequiredField_Rejects()
    {
        var history = new InputHistory(16);
        var chargeTracker = new ChargeTracker(history);
        var participant = new RuntimeStateSnapshotParticipant<InputRuntimeSnapshot>(
            SnapshotParticipantCatalog.Input, 1,
            (_, _) => history.CaptureRuntimeSnapshot(chargeTracker),
            (snapshot, _) => history.PrepareRuntimeSnapshot(snapshot),
            snapshot => history.InstallRuntimeSnapshot(snapshot, chargeTracker));
        var component = new SnapshotComponent(SnapshotParticipantCatalog.Input, 1,
            "{\"P1Directions\":null,\"P1Buttons\":[],\"P2Directions\":[],\"P2Buttons\":[]," +
            "\"ChargeStarts\":[0,0,0,0],\"ChargeEnds\":[0,0,0,0],\"WasCharging\":[false,false,false,false],\"LastUpdateFrame\":0}");
        try
        {
            var ex = Assert.Throws<SnapshotPrepareException>(() => participant.Prepare(
                component, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));
            Assert.Equal(SnapshotParticipantCatalog.Input, ex.FaultPoint);
        }
        finally { history.Shutdown(); }
    }

    [Fact]
    public void InputParticipant_DeclaredCountMismatch_Rejects()
    {
        var history = new InputHistory(16);
        var chargeTracker = new ChargeTracker(history);
        var participant = new RuntimeStateSnapshotParticipant<InputRuntimeSnapshot>(
            SnapshotParticipantCatalog.Input, 1,
            (_, _) => history.CaptureRuntimeSnapshot(chargeTracker),
            (snapshot, _) => history.PrepareRuntimeSnapshot(snapshot),
            snapshot => history.InstallRuntimeSnapshot(snapshot, chargeTracker));
        var component = new SnapshotComponent(SnapshotParticipantCatalog.Input, 1,
            "{\"P1Directions\":[],\"P1Buttons\":[],\"P2Directions\":[],\"P2Buttons\":[]," +
            "\"ChargeStarts\":[0,0,0],\"ChargeEnds\":[0,0,0],\"WasCharging\":[false,false,false],\"LastUpdateFrame\":0}");
        try
        {
            var ex = Assert.Throws<SnapshotPrepareException>(() => participant.Prepare(
                component, new SnapshotPrepareContext(1, 2, 0, SnapshotRestoreMode.Normal)));
            Assert.Equal(SnapshotParticipantCatalog.Input, ex.FaultPoint);
            Assert.Contains("shape", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { history.Shutdown(); }
    }

    [Fact]
    public void TryInspect_CorruptFile_ReturnsActionableError()
    {
        (TrainingStateService service, _, _) = Runtime();
        string path = Path.Combine(_directory, "bad.json");
        File.WriteAllText(path, "garbage");

        Assert.False(service.TryInspect(path, out var info, out string error));
        Assert.Null(info);
        Assert.Contains("JSON", error, StringComparison.Ordinal);
    }

    private sealed class FakeParticipant : IStateSnapshotParticipant
    {
        private readonly SnapshotReference<FakeDto> _slot;
        public FakeParticipant(string discriminator, string value)
        {
            Discriminator = discriminator;
            _slot = new SnapshotReference<FakeDto>(new FakeDto(value));
        }

        public string Discriminator { get; }
        public int CodecVersion => 1;
        public string LiveValue { get => _slot.Value.Value; set => _slot.Value = new FakeDto(value); }
        public bool FailPrepare { get; set; }
        public SnapshotComponent Capture(int frame, ulong epoch) => new(Discriminator, CodecVersion,
            System.Text.Json.JsonSerializer.Serialize(_slot.Value));
        public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
        {
            if (FailPrepare) throw new SnapshotPrepareException(Discriminator, "injected prepare failure");
            FakeDto decoded = System.Text.Json.JsonSerializer.Deserialize<FakeDto>(component.Payload)
                ?? throw new SnapshotPrepareException(Discriminator, "null");
            return PreparedSnapshotComponent.Create(Discriminator, _slot, decoded);
        }

        private sealed record FakeDto(string Value);
    }
}
