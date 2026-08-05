#nullable enable
using System;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests.Replay;

/// <summary>
/// S4.2-B: non-observable bootstrap (ReplayBootstrap) and recording from
/// snapshot.frame + 1 (S4.2-AC01/AC05/AC12/AC13).
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class StateScopedReplayBootstrapTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.All);
    private readonly string _savePath = Path.Combine(Path.GetTempPath(), $"ftg-s42b-save-{Guid.NewGuid():N}.json");
    private readonly string _replayPath = Path.Combine(Path.GetTempPath(), $"ftg-s42b-replay-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
        if (File.Exists(_replayPath)) File.Delete(_replayPath);
        _eventBusScope.Dispose();
    }

    private static StateSnapshot SaveSnapshot(string value = "recorded", int frame = 4, ulong sourceEpoch = 7) =>
        new(1, "2.3.0", sourceEpoch, frame, [new SnapshotComponent("owner", 1, $"{{\"Value\":\"{value}\"}}")]);

    private static JsonStateSnapshotParticipant<OwnerState> Participant(SnapshotReference<OwnerState> slot) =>
        new("owner", 1, slot, (state, _) => state);

    private sealed record OwnerState(string Value);

    [Fact]
    public void TryStartStateScopedRecording_RestoresNonObservable_FirstEnvelopeAtFPlusOne()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        TrainingStatePersistence.Save(SaveSnapshot(), _savePath);
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        int stateRestoredCount = 0;
        EventBus.Instance.Subscribe<StateRestoredEvent>(_ => stateRestoredCount++);
        Assert.True(orchestrator.TryStartStateScopedRecording(_savePath, out string error), error);

        Assert.True(orchestrator.IsRecording);
        Assert.Equal("recorded", slot.Value.Value);
        Assert.Equal(0, stateRestoredCount);
        Assert.Equal(5, EventBus.Instance.CurrentFrame);

        EventBus.Instance.ProcessFrame();
        ReplayFile file = orchestrator.StopRecording();
        Assert.Single(file.Entries);
        Assert.Equal(5, file.Entries[0].Frame);
        Assert.Equal("FrameAdvancedEvent", file.Entries[0].EventType);
    }

    [Fact]
    public void TryStartStateScopedRecording_SaveDigestIsStampedByteExactHash()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        TrainingStatePersistence.Save(SaveSnapshot(), _savePath);
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        Assert.True(orchestrator.TryStartStateScopedRecording(_savePath, out string error), error);
        ReplayFile file = orchestrator.StopRecording();

        byte[] containerBytes = TrainingStatePersistence.ExtractVerifiedContainer(
            File.ReadAllBytes(_savePath), out string digest);
        Assert.Equal(containerBytes, file.InitialSnapshot);
        Assert.Equal(digest, file.InitialSnapshotHash);
    }

    [Fact]
    public void TryStartStateScopedRecording_RejectsWhenTrainingInputOwnsSession()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        TrainingStatePersistence.Save(SaveSnapshot(), _savePath);
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);
        modes.Enter(RuntimePlaybackMode.TrainingInput, 1);

        Assert.False(orchestrator.TryStartStateScopedRecording(_savePath, out string error));

        Assert.Contains("TrainingInput", error, StringComparison.Ordinal);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
    }

    [Fact]
    public void TryStartStateScopedRecording_RejectsWhenCaptureOwnsSession()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        TrainingStatePersistence.Save(SaveSnapshot(), _savePath);
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);
        Assert.True(modes.TryEnterCapture(1, 1, out _));

        Assert.False(orchestrator.TryStartStateScopedRecording(_savePath, out string error));

        Assert.Contains("capture", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
    }

    [Fact]
    public void TryStartStateScopedRecording_RejectsWhenAlreadyRecording()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        TrainingStatePersistence.Save(SaveSnapshot(), _savePath);
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);
        orchestrator.StartRecording();

        Assert.False(orchestrator.TryStartStateScopedRecording(_savePath, out string error));

        Assert.Contains("recording", error, StringComparison.OrdinalIgnoreCase);
        Assert.True(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
    }

    [Fact]
    public void TryStartStateScopedRecording_RejectsWhenPlaybackActive()
    {
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes);
        ReplayCodec.Write(_replayPath, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1, []));
        orchestrator.LoadAndStartReplay(_replayPath);

        Assert.False(orchestrator.TryStartStateScopedRecording(_replayPath, out string error));
        Assert.Contains("replay", error, StringComparison.OrdinalIgnoreCase);
        Assert.True(orchestrator.IsPlaying);
        orchestrator.Stop();
        Assert.False(orchestrator.IsPlaying);
    }

    [Fact]
    public void TryStartStateScopedRecording_GraphRejectionLeavesNoMutation()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)],
            graphValidator: (_, _) => throw new SnapshotPrepareException("cross_component_graph", "frame mismatch"));
        TrainingStatePersistence.Save(SaveSnapshot(), _savePath);
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;

        Assert.False(orchestrator.TryStartStateScopedRecording(_savePath, out string error));

        Assert.Contains("graph", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
    }

    [Fact]
    public void ValidateSnapshot_IsNonMutating()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;

        coordinator.ValidateSnapshot(SaveSnapshot(), SnapshotRestoreMode.ReplayBootstrap);

        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
    }

    [Fact]
    public void LoadAndStartReplay_GraphIncompatibleSnapshot_RejectedBeforeOwnershipAcquisition()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)],
            graphValidator: (_, _) => throw new SnapshotPrepareException("cross_component_graph", "incompatible"));
        byte[] initialBytes = StateSnapshotCodec.Encode(SaveSnapshot(frame: 4));
        ReplayCodec.Write(_replayPath, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 6,
            [new ReplayEntry(5, "FrameAdvancedEvent", "{\"FrameNumber\":5}", 1, 0, 7)],
            initialBytes, ReplayFile.ComputeInitialSnapshotHash(initialBytes)));
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);

        Assert.Throws<SnapshotPrepareException>(() => orchestrator.LoadAndStartReplay(_replayPath));

        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.Equal("live", slot.Value.Value);
        Assert.False(orchestrator.IsPlaying);
    }

    [Fact]
    public void LoadAndStartReplay_HashMismatch_RejectedBeforeOwnershipAcquisition()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        byte[] initialBytes = StateSnapshotCodec.Encode(SaveSnapshot(frame: 4));
        ReplayCodec.Write(_replayPath, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 6,
            [new ReplayEntry(5, "FrameAdvancedEvent", "{\"FrameNumber\":5}", 1, 0, 7)],
            initialBytes, new string('e', 64)));
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);

        Assert.Throws<InvalidDataException>(() => orchestrator.LoadAndStartReplay(_replayPath));

        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.Equal("live", slot.Value.Value);
        Assert.False(orchestrator.IsPlaying);
    }
}
