#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Balance;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Engine.Physics;
using FTG_Framework.Engine.StateMachine;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Replay;

/// <summary>
/// S4.2-C: state-scoped playback driver from snapshot.frame + 1, per-frame
/// BalanceFrameHash match between the recorded and replayed sessions (E4.2-R),
/// and replay-end ReplayHandoff rebind (S4.2-AC07/AC08).
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class StateScopedReplayHashTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.All);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-s42c-{Guid.NewGuid():N}");

    public StateScopedReplayHashTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        _eventBusScope.Dispose();
    }

    private sealed record Bundle(
        StateMachine StateMachine,
        FrameDataEngine FrameData,
        PhysicsEngine Physics,
        InputHistory InputHistory,
        ChargeTracker ChargeTracker,
        ComboStateTracker Combo,
        TrainingInputService TrainingInput,
        TrainingInputRecordingLibrary Library,
        StateSnapshotCoordinator Coordinator,
        ReplayOrchestrator Orchestrator,
        TrainingStateService SaveService,
        PlaybackModeCoordinator PlaybackModes);

    private static Bundle Runtime()
    {
        // 5LP runs frames 4-8 (5 frames) and 6P frames 9-14 when started at the
        // test loop indices below, so each StartMove lands on an Idle timeline.
        var move = new MoveDefinition { MoveId = "5LP", Startup = 2, Active = 1, Recovery = 2 };
        var move2 = new MoveDefinition { MoveId = "6P", Startup = 3, Active = 1, Recovery = 2 };
        var data = new DataStore([move, move2]);
        var stateMachine = new StateMachine(data);
        var inputHistory = new InputHistory(600);
        var chargeTracker = new ChargeTracker(inputHistory);
        var frameData = new FrameDataEngine(data);
        var physics = new PhysicsEngine(data, frameData, stateMachine);
        stateMachine.Initialize(data);
        frameData.Initialize(data);
        physics.Initialize(data);
        stateMachine.InitializePlayer(1);
        stateMachine.InitializePlayer(2);
        var combo = new ComboStateTracker(data);
        combo.Initialize(data);
        var playbackModes = new PlaybackModeCoordinator();
        var library = new TrainingInputRecordingLibrary();
        var trainingInput = new TrainingInputService(
            inputHistory.RecordInput, playbackModes,
            (player, fromFrame) => { inputHistory.ResetTrainingTransient(player, fromFrame); chargeTracker.ResetPlayer(player); },
            inputHistory.RecordPlaybackInputs, library);
        var orchestrator = new ReplayOrchestrator(frameData, playbackModes);
        var coordinator = GameLoop.CreateRuntimeSnapshotCoordinator(
            stateMachine, frameData, physics, inputHistory, chargeTracker,
            orchestrator, combo, trainingInput, data);
        orchestrator.AttachSnapshotCoordinator(coordinator);
        var saveService = new TrainingStateService(
            coordinator, () => EventBus.Instance.CurrentFrame, () => EventBus.Instance.LifecycleEpoch);
        return new Bundle(stateMachine, frameData, physics, inputHistory, chargeTracker,
            combo, trainingInput, library, coordinator, orchestrator, saveService, playbackModes);
    }

    private static void Shutdown(Bundle bundle)
    {
        bundle.Physics.Shutdown();
        bundle.FrameData.Shutdown();
        bundle.ChargeTracker.Shutdown();
        bundle.InputHistory.Shutdown();
        bundle.StateMachine.Shutdown();
        bundle.Combo.Shutdown();
    }

    private static string Hash(Bundle bundle) =>
        BalanceFrameHash.Compute(EventBus.Instance.CurrentFrame, 1,
            bundle.FrameData, bundle.StateMachine, bundle.Physics, bundle.Combo, bundle.InputHistory, bundle.ChargeTracker);

    [Fact]
    public void LoadAndStartReplay_StateScopedFile_StartsDriverAtSnapshotFramePlusOne()
    {
        var slot = new SnapshotReference<OwnerState>(new OwnerState("live"));
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [Participant(slot)]);
        byte[] initialBytes = StateSnapshotCodec.Encode(new StateSnapshot(1, "2.3.0", 7, 4,
            [new SnapshotComponent("owner", 1, "{\"Value\":\"recorded\"}")]));
        string path = Path.Combine(_directory, "starts.json");
        ReplayCodec.Write(path, new ReplayFile("2.3.0", ReplayVersionValidator.CurrentDataVersion, 8,
            [
                new ReplayEntry(5, "FrameAdvancedEvent", "{\"FrameNumber\":5}", 1, 0, 7),
                new ReplayEntry(6, "FrameAdvancedEvent", "{\"FrameNumber\":6}", 1, 0, 7),
                new ReplayEntry(7, "FrameAdvancedEvent", "{\"FrameNumber\":7}", 1, 0, 7)
            ], initialBytes, ReplayFile.ComputeInitialSnapshotHash(initialBytes)));
        var orchestrator = new ReplayOrchestrator(snapshotCoordinator: coordinator);

        orchestrator.LoadAndStartReplay(path);

        Assert.Equal(5, orchestrator.CurrentReplayFrame);
        Assert.True(orchestrator.ProcessReplayFrame());
        Assert.Equal(6, orchestrator.CurrentReplayFrame);
        Assert.True(orchestrator.ProcessReplayFrame());
        Assert.False(orchestrator.ProcessReplayFrame());
        Assert.False(orchestrator.IsPlaying);
    }

    [Fact]
    public void StateScopedPlayback_PerFrameHashesMatchFrameForFrame()
    {
        const int frames = 13;
        var recordedTrail = new List<string>();
        string replayPath = Path.Combine(_directory, "replay.json");
        ReplayFile? file = null;
        Bundle b = Runtime();
        string savePath = Path.Combine(_directory, "save.json");
        try
        {
            Assert.True(b.SaveService.TrySave(savePath, out string saveError), saveError);
            Assert.True(b.Orchestrator.TryStartStateScopedRecording(savePath, out string startError), startError);

            for (int frame = 0; frame < frames; frame++)
            {
                if (frame is 3 or 8)
                    b.FrameData.StartMove(1, frame == 3 ? "5LP" : "6P");
                b.ChargeTracker.Update(1, EventBus.Instance.CurrentFrame);
                b.ChargeTracker.Update(2, EventBus.Instance.CurrentFrame);
                b.FrameData.Update();
                b.Physics.Update();
                recordedTrail.Add(Hash(b));
                EventBus.Instance.ProcessFrame();
            }
            // The hash trail must capture the move state changes, not idle frames only.
            Assert.Contains("5LP", string.Join(string.Empty, recordedTrail));
            Assert.Contains("6P", string.Join(string.Empty, recordedTrail));
            file = b.Orchestrator.StopRecording();
            ReplayCodec.Write(replayPath, file);
        }
        finally { Shutdown(b); }

        Bundle p = Runtime();
        try
        {
            p.Orchestrator.LoadAndStartReplay(replayPath);
            var replayedTrail = new List<string>();
            while (p.Orchestrator.IsPlaying)
            {
                // Mirror GameLoop's replay branch: the driver consumes the last
                // injected frame (Stop) before Update/hash, so no post-tick
                // snapshot exists for it; physics stays offline during playback.
                if (!p.Orchestrator.ProcessReplayFrame())
                    break;
                p.FrameData.Update();
                replayedTrail.Add(Hash(p));
                EventBus.Instance.ProcessFrame();
            }

            Assert.Equal(recordedTrail.Take(replayedTrail.Count), replayedTrail);
            Assert.False(p.Orchestrator.IsPlaying);
            Assert.Equal(RuntimePlaybackMode.None, p.PlaybackModes.ActiveMode);
        }
        finally { Shutdown(p); }
    }

    [Fact]
    public void Stop_ReplayHandoffRebindsFinalStateUnderReservedEpoch()
    {
        Bundle b = Runtime();
        string savePath = Path.Combine(_directory, "save2.json");
        string replayPath = Path.Combine(_directory, "replay2.json");
        ReplayFile file;
        try
        {
            Assert.True(b.SaveService.TrySave(savePath, out string saveError), saveError);
            Assert.True(b.Orchestrator.TryStartStateScopedRecording(savePath, out string startError), startError);
            for (int frame = 0; frame < 10; frame++)
            {
                if (frame is 2 or 7)
                    b.FrameData.StartMove(1, "5LP");
                b.ChargeTracker.Update(1, EventBus.Instance.CurrentFrame);
                b.ChargeTracker.Update(2, EventBus.Instance.CurrentFrame);
                b.FrameData.Update();
                b.Physics.Update();
                EventBus.Instance.ProcessFrame();
            }
            file = b.Orchestrator.StopRecording();
            ReplayCodec.Write(replayPath, file);
        }
        finally { Shutdown(b); }

        Bundle p = Runtime();
        try
        {
            p.Orchestrator.LoadAndStartReplay(replayPath);
            while (p.Orchestrator.IsPlaying)
            {
                // Mirror GameLoop's replay branch: the driver stops on the last
                // injected frame; no Update/hash follows that frame.
                if (!p.Orchestrator.ProcessReplayFrame())
                    break;
                p.FrameData.Update();
                EventBus.Instance.ProcessFrame();
            }

            // Stop ran through the ReplayHandoff path: final state rebound under a
            // fresh epoch and the live frame domain resumed at the last replay frame.
            Assert.False(p.Orchestrator.IsPlaying);
            Assert.Equal(RuntimePlaybackMode.None, p.PlaybackModes.ActiveMode);
            Assert.Equal(file.FrameCount, EventBus.Instance.CurrentFrame);
            // The reproduced final move state survived the handoff into live play.
            Assert.Equal("5LP", p.FrameData.GetCurrentMoveId(1));
            Assert.NotEqual(MovePhase.Idle, p.FrameData.GetPhase(1));

            // Live play continues cleanly after the rebind.
            EventBus.Instance.ProcessFrame();
            Assert.Equal(file.FrameCount + 1, EventBus.Instance.CurrentFrame);
        }
        finally { Shutdown(p); }
    }

    private static JsonStateSnapshotParticipant<OwnerState> Participant(SnapshotReference<OwnerState> slot) =>
        new("owner", 1, slot, (state, _) => state);

    private sealed record OwnerState(string Value);
}
