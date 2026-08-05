#nullable enable
using System;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Replay;
using Xunit;

namespace FTG_Framework.Tests.Replay;

[Collection(EventBusTestCollection.Name)]
public sealed class ReplayOrchestratorTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.All);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ftg-replay-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        _eventBusScope.Dispose();
    }

    [Fact]
    public void LoadAndStartReplay_ConsumesInitialSnapshotAndOwnsReboundEpoch()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        var initial = new StateSnapshot(1, "2.3.0", 17, 4,
            [new SnapshotComponent("owner", 1, "{\"Value\":\"recorded\"}")]);
        byte[] initialBytes = StateSnapshotCodec.Encode(initial);
        ReplayCodec.Write(_path, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion,
            1, [], initialBytes, ReplayFile.ComputeInitialSnapshotHash(initialBytes)));
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);

        orchestrator.LoadAndStartReplay(_path);

        Assert.True(orchestrator.IsPlaying);
        Assert.Equal("recorded", slot.Value.Value);
        Assert.Equal(RuntimePlaybackMode.AuthoritativeReplay, modes.ActiveMode);
        Assert.Equal(EventBus.Instance.LifecycleEpoch, modes.ActiveEpoch);
        Assert.Equal(5, EventBus.Instance.CurrentFrame);
        orchestrator.Stop();
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
    }

    [Fact]
    public void LoadAndStartReplay_PrepareFailureRollsBackModeAndBusPlaybackFlags()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var failing = new JsonStateSnapshotParticipant<OwnerState>("owner", 1, slot,
            (_, _) => throw new InvalidDataException("fault"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [failing]);
        var initial = new StateSnapshot(1, "2.3.0", 1, 0,
            [new SnapshotComponent("owner", 1, "{\"Value\":\"recorded\"}")]);
        byte[] initialBytes = StateSnapshotCodec.Encode(initial);
        ReplayCodec.Write(_path, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion,
            1, [], initialBytes, ReplayFile.ComputeInitialSnapshotHash(initialBytes)));
        var recorder = new StubRecorder();
        EventBus.Instance.Recorder = recorder;
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);

        Assert.Throws<SnapshotPrepareException>(() => orchestrator.LoadAndStartReplay(_path));

        Assert.False(orchestrator.IsPlaying);
        Assert.Same(recorder, EventBus.Instance.Recorder);
        Assert.False(EventBus.Instance.SuppressFrameAdvanced);
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.Equal("live", slot.Value.Value);
    }

    [Fact]
    public void LoadAndStartReplay_ModeConflictOccursBeforeBusMutation()
    {
        ReplayCodec.Write(_path, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 1, []));
        var recorder = new StubRecorder();
        EventBus.Instance.Recorder = recorder;
        var modes = new PlaybackModeCoordinator();
        modes.Enter(RuntimePlaybackMode.TrainingInput, EventBus.Instance.LifecycleEpoch);
        var orchestrator = new ReplayOrchestrator(playbackModes: modes);

        Assert.Throws<InvalidOperationException>(() => orchestrator.LoadAndStartReplay(_path));

        Assert.Same(recorder, EventBus.Instance.Recorder);
        Assert.False(EventBus.Instance.SuppressFrameAdvanced);
        Assert.Equal(RuntimePlaybackMode.TrainingInput, modes.ActiveMode);
    }

    [Fact]
    public void StopRecording_StampsInitialSnapshotHashOverCapturedBytes()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        orchestrator.StartRecording();
        ReplayFile file = orchestrator.StopRecording();

        Assert.NotNull(file.InitialSnapshot);
        Assert.Equal(ReplayFile.ComputeInitialSnapshotHash(file.InitialSnapshot), file.InitialSnapshotHash);
    }

    [Fact]
    public void StopRecording_WithoutCoordinatorLeavesSnapshotAndHashNull()
    {
        var orchestrator = new ReplayOrchestrator();
        orchestrator.StartRecording();
        ReplayFile file = orchestrator.StopRecording();
        Assert.Null(file.InitialSnapshot);
        Assert.Null(file.InitialSnapshotHash);
    }

    private static JsonStateSnapshotParticipant<OwnerState> Participant(SnapshotReference<OwnerState> slot) =>
        new("owner", 1, slot, (state, _) => state);

    private sealed record OwnerState(string Value);

    private sealed class StubRecorder : IReplayRecorder
    {
        public bool IsRecording { get; set; }
        public int MaxFrameNumber => 0;
        public int EventCount => 0;
        public void Record<T>(int frame, T evt) { }
        public ReplayFile Save() => new("2.3.0", ReplayVersionValidator.CurrentDataVersion, 0, []);
    }
}
