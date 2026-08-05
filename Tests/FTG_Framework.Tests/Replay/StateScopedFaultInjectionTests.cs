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
/// S4.2-D: failure atomicity (AC09) across every state-scoped bootstrap/
/// validation/persistence seam — failure leaves active state, epoch, queues,
/// and the original save/replay files unchanged, with a component/stage
/// diagnostic — plus standalone parity (AC10): Normal restore keeps its
/// phase-7 StateRestored while ReplayBootstrap/ReplayHandoff stay silent.
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class StateScopedFaultInjectionTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.All);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-s42d-{Guid.NewGuid():N}");

    public StateScopedFaultInjectionTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        _eventBusScope.Dispose();
    }

    private static StateSnapshot SaveSnapshot(string value = "recorded", int frame = 4, ulong sourceEpoch = 7) =>
        new(1, "2.3.0", sourceEpoch, frame, [new SnapshotComponent("owner", 1, $"{{\"Value\":\"{value}\"}}")]);

    private static JsonStateSnapshotParticipant<OwnerState> Participant(SnapshotReference<OwnerState> slot) =>
        new("owner", 1, slot, (state, _) => state);

    private sealed record OwnerState(string Value);

    [Theory]
    [InlineData(SnapshotFaultPoint.ReserveEpoch)]
    [InlineData(SnapshotFaultPoint.DecodeComponent)]
    [InlineData(SnapshotFaultPoint.PrepareParticipant)]
    [InlineData(SnapshotFaultPoint.ValidateGraph)]
    public void TryStartStateScopedRecording_EveryRestoreFaultPoint_LeavesLiveStateEpochQueuesAndSaveFileUnchanged(SnapshotFaultPoint faultPoint)
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)],
            faultInjector: (point, _) =>
            {
                if (point == faultPoint) throw new InvalidOperationException($"injected:{faultPoint}");
            });
        string savePath = Path.Combine(_directory, $"save-{faultPoint}.json");
        TrainingStatePersistence.Save(SaveSnapshot(), savePath);
        byte[] fileBefore = File.ReadAllBytes(savePath);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;
        SnapshotAtomicityDiagnostic atomicityBefore = EventBus.Instance.GetSnapshotAtomicityDiagnostic();
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        Assert.False(orchestrator.TryStartStateScopedRecording(savePath, out string error));

        // The diagnostic identifies the failed stage (S4.2-AC09).
        Assert.Contains(faultPoint.ToString(), error, StringComparison.Ordinal);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
        SnapshotAtomicityDiagnostic atomicityAfter = EventBus.Instance.GetSnapshotAtomicityDiagnostic();
        Assert.Equal(atomicityBefore.ActiveEpoch, atomicityAfter.ActiveEpoch);
        Assert.Null(atomicityAfter.ReservedEpoch);
        Assert.Equal(atomicityBefore.Current, atomicityAfter.Current);
        Assert.Equal(atomicityBefore.Next, atomicityAfter.Next);
        Assert.Equal(atomicityBefore.Pending, atomicityAfter.Pending);
        Assert.Equal(atomicityBefore.Quarantined, atomicityAfter.Quarantined);
        Assert.Equal(fileBefore, File.ReadAllBytes(savePath));
    }

    [Fact]
    public void TryStartStateScopedRecording_UnreadableSaveFile_LeavesLiveStateUnchanged()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        Assert.False(orchestrator.TryStartStateScopedRecording(
            Path.Combine(_directory, "missing.json"), out string error));

        Assert.Contains("Cannot read", error, StringComparison.Ordinal);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
    }

    [Fact]
    public void TryStartStateScopedRecording_TamperedSaveFile_LeavesLiveStateAndFileUnchanged()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        string savePath = Path.Combine(_directory, "tampered.json");
        TrainingStatePersistence.Save(SaveSnapshot(), savePath);
        byte[] tampered = File.ReadAllBytes(savePath);
        tampered[^1] ^= 0xFF;
        File.WriteAllBytes(savePath, tampered);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        Assert.False(orchestrator.TryStartStateScopedRecording(savePath, out string error));

        Assert.Contains("integrity", error, StringComparison.Ordinal);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
        Assert.Equal(tampered, File.ReadAllBytes(savePath));
    }

    [Fact]
    public void TryStartStateScopedRecording_UnknownComponent_ReportsComponentAndLeavesLiveStateUnchanged()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        string savePath = Path.Combine(_directory, "unknown.json");
        TrainingStatePersistence.Save(new StateSnapshot(1, "2.3.0", 7, 4,
            [new SnapshotComponent("owner", 1, "{\"Value\":\"recorded\"}"),
             new SnapshotComponent("unknown_component", 1, "{}")]), savePath);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        Assert.False(orchestrator.TryStartStateScopedRecording(savePath, out string error));

        // The diagnostic identifies the incompatible component (S4.2-AC09).
        Assert.Contains("unknown_component", error, StringComparison.Ordinal);
        Assert.False(orchestrator.IsRecording);
        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
    }

    [Theory]
    [InlineData(SnapshotFaultPoint.ReserveEpoch)]
    [InlineData(SnapshotFaultPoint.DecodeComponent)]
    [InlineData(SnapshotFaultPoint.PrepareParticipant)]
    [InlineData(SnapshotFaultPoint.ValidateGraph)]
    public void LoadAndStartReplay_EveryRestoreFaultPoint_UnwindsOwnershipAndPreservesReplayFile(SnapshotFaultPoint faultPoint)
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)],
            faultInjector: (point, _) =>
            {
                if (point == faultPoint) throw new InvalidOperationException($"injected:{faultPoint}");
            });
        byte[] initialBytes = StateSnapshotCodec.Encode(SaveSnapshot(frame: 4));
        string replayPath = Path.Combine(_directory, $"replay-{faultPoint}.json");
        ReplayCodec.Write(replayPath, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 6,
            [new ReplayEntry(5, "FrameAdvancedEvent", "{\"FrameNumber\":5}", 1, 0, 7)],
            initialBytes, ReplayFile.ComputeInitialSnapshotHash(initialBytes)));
        byte[] fileBefore = File.ReadAllBytes(replayPath);
        ulong epochBefore = EventBus.Instance.LifecycleEpoch;
        var modes = new PlaybackModeCoordinator();
        var orchestrator = new ReplayOrchestrator(playbackModes: modes, snapshotCoordinator: coordinator);

        var ex = Assert.Throws<SnapshotPrepareException>(() => orchestrator.LoadAndStartReplay(replayPath));

        Assert.Contains(faultPoint.ToString(), ex.Message, StringComparison.Ordinal);
        Assert.Equal(RuntimePlaybackMode.None, modes.ActiveMode);
        Assert.False(orchestrator.IsPlaying);
        Assert.Equal("live", slot.Value.Value);
        Assert.Equal(epochBefore, EventBus.Instance.LifecycleEpoch);
        Assert.False(EventBus.Instance.SuppressFrameAdvanced);
        Assert.False(EventBus.Instance.GetTestDiagnostic().ReplayApplyActive);
        Assert.Equal(fileBefore, File.ReadAllBytes(replayPath));
    }

    [Fact]
    public void Stop_ReplayHandoffRestore_EmitsNoStateRestored()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        byte[] initialBytes = StateSnapshotCodec.Encode(SaveSnapshot(value: "captured", frame: 4));
        string replayPath = Path.Combine(_directory, "handoff.json");
        ReplayCodec.Write(replayPath, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 6,
            [new ReplayEntry(5, "FrameAdvancedEvent", "{\"FrameNumber\":5}", 1, 0, 7)],
            initialBytes, ReplayFile.ComputeInitialSnapshotHash(initialBytes)));
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);
        int stateRestoredCount = 0;
        Action<StateRestoredEvent> handler = _ => stateRestoredCount++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            orchestrator.LoadAndStartReplay(replayPath);
            while (orchestrator.IsPlaying)
            {
                if (!orchestrator.ProcessReplayFrame())
                    break;
                EventBus.Instance.ProcessFrame();
            }

            // ReplayHandoff restore is non-observable: zero StateRestored across
            // the whole playback session including the end rebind (S4.2-AC10).
            Assert.Equal(0, stateRestoredCount);
            Assert.False(orchestrator.IsPlaying);
            Assert.Equal("captured", slot.Value.Value);
            EventBus.Instance.ProcessFrame();
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void NormalRestore_EmitsPhase7StateRestored_WhileReplayModesStaySilent()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        StateSnapshot snapshot = coordinator.Capture(3, "2.3.0");
        slot.Value = new OwnerState("changed");
        int stateRestoredCount = 0;
        Action<StateRestoredEvent> handler = _ => stateRestoredCount++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            coordinator.Restore(snapshot, SnapshotRestoreMode.Normal);
            Assert.Equal(1, stateRestoredCount);
            Assert.Equal("live", slot.Value.Value);

            coordinator.Restore(snapshot, SnapshotRestoreMode.ReplayBootstrap);
            coordinator.Restore(snapshot, SnapshotRestoreMode.ReplayHandoff);
            Assert.Equal(1, stateRestoredCount);
            Assert.Equal("live", slot.Value.Value);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }
}
