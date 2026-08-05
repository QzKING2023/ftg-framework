#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Engine.Physics;
using FTG_Framework.Engine.StateMachine;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public sealed class GameLoopTrainingStateTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-runtime-save-{Guid.NewGuid():N}");

    public GameLoopTrainingStateTests() => Directory.CreateDirectory(_directory);
    public void Dispose()
    {
        _eventBusScope.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private sealed record RuntimeBundle(
        StateMachine StateMachine, FrameDataEngine FrameData, PhysicsEngine Physics,
        InputHistory InputHistory, ChargeTracker ChargeTracker, ComboStateTracker Combo,
        TrainingInputService TrainingInput, TrainingInputRecordingLibrary Library,
        StateSnapshotCoordinator Coordinator, ReplayOrchestrator Orchestrator,
        TrainingStateService SaveService);

    private RuntimeBundle Runtime()
    {
        var move = new MoveDefinition { MoveId = "5LP", Startup = 3, Active = 2, Recovery = 4 };
        var data = new DataStore([move]);
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
        var saveService = new TrainingStateService(
            coordinator, () => EventBus.Instance.CurrentFrame, () => EventBus.Instance.LifecycleEpoch);
        return new RuntimeBundle(stateMachine, frameData, physics, inputHistory, chargeTracker,
            combo, trainingInput, library, coordinator, orchestrator, saveService);
    }

    private static void Shutdown(RuntimeBundle bundle)
    {
        bundle.Physics.Shutdown();
        bundle.FrameData.Shutdown();
        bundle.ChargeTracker.Shutdown();
        bundle.InputHistory.Shutdown();
        bundle.StateMachine.Shutdown();
        bundle.Combo.Shutdown();
    }

    [Fact]
    public void SaveRestore_RoundTripsMoveInputComboAndPlayback_ExactlyOnceStateRestored()
    {
        RuntimeBundle bundle = Runtime();
        string path = Path.Combine(_directory, "mid-combo.json");
        int restored = 0;
        Action<StateRestoredEvent> handler = _ => restored++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            // Drive a deterministic pre-capture state: P1 in a move, buffered
            // inputs, an active combo track, and training playback mid-session.
            bundle.InputHistory.RecordInput(1, InputType.Directional, (int)DirectionValue.Back);
            bundle.InputHistory.RecordInput(1, InputType.Button, (int)ButtonValue.A);
            bundle.FrameData.StartMove(1, "5LP");
            bundle.FrameData.Update();
            int capturedMoveFrame = bundle.FrameData.GetCurrentFrame(1);
            EventBus.Instance.Publish(new HitConnectedEvent(1, 2, "5LP", 5, 10));
            EventBus.Instance.ProcessFrame();
            Assert.True(bundle.Combo.IsActive(1));

            var recording = new TrainingInputRecording(1, 1, 20, "dummy",
                new[]
                {
                    new TrainingInputRecordingEntry(0, 0, InputType.Directional, (int)DirectionValue.Down),
                    new TrainingInputRecordingEntry(3, 0, InputType.Button, (int)ButtonValue.A)
                });
            Assert.True(bundle.Library.TryAddOrReplace(recording, false, null, out _));
            ulong epoch = EventBus.Instance.LifecycleEpoch;
            Assert.True(bundle.TrainingInput.TryStartPlayback(
                recording, 2, EventBus.Instance.CurrentFrame, false, epoch, out _));
            bundle.TrainingInput.ProcessPlaybackFrame(EventBus.Instance.CurrentFrame, epoch);
            bundle.TrainingInput.CompleteFrame();

            Assert.True(bundle.SaveService.TrySave(path, out string error), error);

            // Mutate everything after capture.
            bundle.FrameData.StartMove(2, "5LP");
            bundle.FrameData.Update();
            bundle.InputHistory.RecordInput(1, InputType.Directional, (int)DirectionValue.Forward);
            bundle.TrainingInput.StopPlayback();
            bundle.Combo.InstallComboState(new ComboRuntimeSnapshot(
                new Dictionary<int, ComboTrackRuntimeSnapshot>()));
            int frameBefore = EventBus.Instance.CurrentFrame;

            Assert.True(bundle.SaveService.TryRestore(path, out error), error);

            Assert.Equal(1, restored);
            Assert.Equal(frameBefore, EventBus.Instance.CurrentFrame);
            Assert.Equal("5LP", bundle.FrameData.GetCurrentMoveId(1));
            Assert.Equal(capturedMoveFrame, bundle.FrameData.GetCurrentFrame(1));
            Assert.True(bundle.Combo.IsActive(1));
            Assert.Equal(1, bundle.Combo.GetHitCount(1));
            Assert.Single(bundle.InputHistory.GetDirectionalHistory(1)!);
            Assert.False(bundle.TrainingInput.IsPlaying, "Install must not activate live playback.");

            // The restored session resumes on the next processed frame.
            bundle.TrainingInput.ProcessPlaybackFrame(EventBus.Instance.CurrentFrame, EventBus.Instance.LifecycleEpoch);
            Assert.True(bundle.TrainingInput.IsPlaying);
            bundle.TrainingInput.CompleteFrame();
            bundle.TrainingInput.StopPlayback();

            // The rest of the frame pipeline still runs cleanly after restore.
            EventBus.Instance.ProcessFrame();
        }
        finally
        {
            EventBus.Instance.Unsubscribe(handler);
            Shutdown(bundle);
        }
    }

    [Fact]
    public void Restore_GraphValidationFailure_PreservesLiveState()
    {
        RuntimeBundle bundle = Runtime();
        string path = Path.Combine(_directory, "invalid.json");
        try
        {
            // Capture a valid snapshot, then craft a candidate whose frame_data
            // component disagrees with the container frame.
            bundle.FrameData.StartMove(1, "5LP");
            bundle.FrameData.Update();
            Assert.True(bundle.SaveService.TrySave(path, out _));
            StateSnapshot loaded = TrainingStatePersistence.Load(path, out _);
            var tampered = new StateSnapshot(loaded.SchemaVersion, loaded.FrameworkVersion,
                loaded.SourceEpoch, loaded.Frame + 5, loaded.Components);
            Assert.Equal(TrainingSaveStatus.Succeeded,
                TrainingStatePersistence.Save(tampered, path).Status);

            string[] before = new[]
            {
                bundle.FrameData.GetCurrentMoveId(1) ?? string.Empty,
                bundle.FrameData.GetCurrentFrame(1).ToString()
            };
            ulong epoch = EventBus.Instance.LifecycleEpoch;

            Assert.False(bundle.SaveService.TryRestore(path, out string error));
            Assert.Contains("frame", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before[0], bundle.FrameData.GetCurrentMoveId(1) ?? string.Empty);
            Assert.Equal(before[1], bundle.FrameData.GetCurrentFrame(1).ToString());
            Assert.Equal(epoch, EventBus.Instance.LifecycleEpoch);
        }
        finally { Shutdown(bundle); }
    }

    [Fact]
    public void Restore_ObserverThrow_DoesNotRollbackCommittedState()
    {
        RuntimeBundle bundle = Runtime();
        try
        {
            bundle.FrameData.StartMove(1, "5LP");
            bundle.FrameData.Update();
            int capturedFrame = bundle.FrameData.GetCurrentFrame(1);
            int restored = 0;
            bool threw = false;
            Action<StateRestoredEvent> handler = _ =>
            {
                restored++;
                threw = true;
                throw new InvalidOperationException("observer boom");
            };
            EventBus.Instance.Subscribe(handler);
            try
            {
                string path = Path.Combine(_directory, "observer.json");
                Assert.True(bundle.SaveService.TrySave(path, out _));
                Assert.True(bundle.SaveService.TryRestore(path, out string error), error);
            }
            finally { EventBus.Instance.Unsubscribe(handler); }

            Assert.True(threw);
            Assert.Equal(1, restored);
            Assert.Equal(capturedFrame, bundle.FrameData.GetCurrentFrame(1));
            Assert.Equal("5LP", bundle.FrameData.GetCurrentMoveId(1));
        }
        finally { Shutdown(bundle); }
    }

    [Fact]
    public void Prepare_ReservesEpochWithoutActivating_AndFailureReleasesQuarantineAgainstOldEpoch()
    {
        RuntimeBundle bundle = Runtime();
        try
        {
            string path = Path.Combine(_directory, "prepare.json");
            Assert.True(bundle.SaveService.TrySave(path, out _));
            ulong activeBefore = EventBus.Instance.LifecycleEpoch;

            // A second coordinator with a failing graph validator rejects the
            // candidate after epoch reservation, inside Prepare.
            var failingCoordinator = new StateSnapshotCoordinator(
                EventBus.Instance, bundle.Coordinator.ParticipantOrder
                    .Select(id => (IStateSnapshotParticipant)new ProbeParticipant(id, "v")),
                graphValidator: (_, _) =>
                    throw new SnapshotPrepareException("cross_component_graph", "injected graph failure"));
            StateSnapshot snapshot = TrainingStatePersistence.Load(path, out _);

            Assert.Throws<SnapshotPrepareException>(() =>
                failingCoordinator.Restore(snapshot, SnapshotRestoreMode.Normal));

            Assert.Equal(activeBefore, EventBus.Instance.LifecycleEpoch);
            SnapshotAtomicityDiagnostic diagnostic = EventBus.Instance.GetSnapshotAtomicityDiagnostic();
            Assert.Null(diagnostic.ReservedEpoch);
        }
        finally { Shutdown(bundle); }
    }

    [Fact]
    public void RestartPortability_NewSession_MatchesOriginalOverSixHundredFrames()
    {
        const int captureAt = 30;
        const int window = 600;
        string path = Path.Combine(_directory, "portable.json");

        (string first, ulong firstGeneration) = RunSession(path, restoreAt: null, captureAt);
        (string second, ulong secondGeneration) = RunSession(path, restoreAt: captureAt, captureAt);

        Assert.Equal(firstGeneration, secondGeneration);
        Assert.True(firstGeneration > 0, "The scenario must produce an in-flight knockback generation.");
        Assert.Equal(first, second);

        static (string Hashes, ulong Generation) RunSession(string savePath, int? restoreAt, int captureAt)
        {
            var move = new MoveDefinition
            {
                MoveId = "5A", Startup = 1, Active = 1, Recovery = 1,
                HitAdvantage = 200, BlockAdvantage = -2, Damage = 10,
                KnockbackProfileId = "launch",
                CollisionFrames = new[]
                {
                    new CollisionFrameDefinition
                    {
                        Frame = 2,
                        Hitboxes = new[] { new CollisionBoxDefinition { BoxId = "hit-a", X = 10, Width = 120, Height = 20 } }
                    }
                }
            };
            var data = new DataStore([move],
                knockbackProfiles: new[] { new KnockbackProfile { ProfileId = "launch", Horizontal = 8, Friction = 0.3f } });
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
            physics.Register(new MutablePhysicsParticipant(1, -5, facingRight: true));
            physics.Register(new MutablePhysicsParticipant(2, 5, facingRight: false, hasHurtbox: true));
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
            var saveService = new TrainingStateService(
                coordinator, () => EventBus.Instance.CurrentFrame, () => EventBus.Instance.LifecycleEpoch);
            var recording = new TrainingInputRecording(1, 1, 90, "dummy",
                new[]
                {
                    new TrainingInputRecordingEntry(0, 0, InputType.Directional, (int)DirectionValue.Down),
                    new TrainingInputRecordingEntry(45, 0, InputType.Button, (int)ButtonValue.A)
                });
            Assert.True(library.TryAddOrReplace(recording, false, null, out _));
            Assert.True(library.TryAssign(2, "dummy", out _));

            try
            {
                // A restarted session runs in a fresh frame domain (new process);
                // rewind the process-global counter so the pre-capture scenario is
                // byte-identical across both sessions.
                EventBus.Instance.RewindFrameCounter(0);
                frameData.StartMove(1, "5A");
                ulong epoch = EventBus.Instance.LifecycleEpoch;
                Assert.True(trainingInput.TryStartPlayback(
                    recording, 2, 0, true, epoch, out _));
                var hashes = new StringBuilder();
                int frame = 0;
                for (; frame <= captureAt + window; frame++)
                {
                    if (frame == captureAt)
                    {
                        Assert.True(saveService.TrySave(savePath, out string error), error);
                        var physicsAtSave = physics.CaptureRuntimeSnapshot();
                        Assert.True(physicsAtSave.Trajectories.TryGetValue(2, out var inFlight) && !inFlight.Completed,
                            "Scenario must have an in-flight knockback at capture.");
                        Assert.True(combo.IsActive(1), "Scenario must have an active combo at capture.");
                        if (restoreAt is not null)
                        {
                            Assert.True(saveService.TryRestore(savePath, out error), error);
                            epoch = EventBus.Instance.LifecycleEpoch;
                        }
                    }
                    trainingInput.ProcessPlaybackFrame(frame, epoch);
                    inputHistory.RecordInput(1, InputType.Directional, (int)DirectionValue.Down);
                    if (frame % 97 == 0)
                        inputHistory.RecordInput(1, InputType.Button, (int)ButtonValue.A);
                    if (frameData.GetPhase(1) == MovePhase.Idle && frame % 60 == 0)
                        frameData.StartMove(1, "5A");
                    chargeTracker.Update(1, frame);
                    chargeTracker.Update(2, frame);
                    frameData.Update();
                    physics.Update();
                    trainingInput.CompleteCaptureFrame(frame);
                    EventBus.Instance.ProcessFrame();
                    trainingInput.CompleteFrame();
                    if (frame >= captureAt && frame < captureAt + window)
                        hashes.Append(HashFrame(frame, frameData, stateMachine, physics, combo, inputHistory, chargeTracker))
                            .Append('\n');
                }
                var physicsEnd = physics.CaptureRuntimeSnapshot();
                ulong highWater = physicsEnd.GenerationHighWater.TryGetValue(2, out ulong value) ? value : 0;
                Assert.True(highWater > 1, "Scenario must have launched a second knockback generation.");
                return (hashes.ToString(), highWater);
            }
            finally
            {
                physics.Shutdown();
                frameData.Shutdown();
                chargeTracker.Shutdown();
                inputHistory.Shutdown();
                stateMachine.Shutdown();
                combo.Shutdown();
            }
        }
    }

    private static string HashFrame(int frame, FrameDataEngine frameData, StateMachine stateMachine,
        PhysicsEngine physics, ComboStateTracker combo, InputHistory inputHistory,
        ChargeTracker chargeTracker)
    {
        var sb = new StringBuilder();
        sb.Append(frame).Append('|');
        var fd = frameData.CaptureRuntimeSnapshot(frame);
        sb.Append(fd.State.P1MoveId).Append('/').Append(fd.State.P1CurrentFrame).Append('/').Append(fd.State.P1Phase).Append('|');
        sb.Append(fd.State.P2MoveId).Append('/').Append(fd.State.P2CurrentFrame).Append('/').Append(fd.State.P2Phase).Append('|');
        var sm = stateMachine.CaptureRuntimeSnapshot();
        foreach (var kv in sm.Stacks.OrderBy(k => k.Key))
            sb.Append(kv.Key).Append(':').Append(string.Join(',', kv.Value)).Append('|');
        var ph = physics.CaptureRuntimeSnapshot();
        foreach (var kv in ph.Trajectories.OrderBy(k => k.Key))
            sb.Append(kv.Key).Append(':').Append(kv.Value.GenerationId).Append(',').Append(kv.Value.PositionX).Append(',').Append(kv.Value.PositionY).Append(',').Append(kv.Value.Completed).Append('|');
        foreach (var kv in ph.GenerationHighWater.OrderBy(k => k.Key))
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append('|');
        var comboState = combo.CaptureComboState();
        foreach (var kv in comboState.Tracks.OrderBy(k => k.Key))
            sb.Append(kv.Key).Append(':').Append(kv.Value.Active).Append(',').Append(kv.Value.HitCount).Append(',').Append(kv.Value.CurrentMoveId).Append(',').Append(kv.Value.StartFrame).Append('|');
        var input = inputHistory.CaptureRuntimeSnapshot(chargeTracker);
        foreach (var entry in input.P1Directions.Skip(Math.Max(0, input.P1Directions.Length - 8)))
            sb.Append(entry.Frame).Append(':').Append((int)entry.Value).Append(',');
        sb.Append(';');
        return sb.ToString();
    }

    private sealed class MutablePhysicsParticipant : IPhysicsParticipant, IRestorablePhysicsParticipant
    {
        public MutablePhysicsParticipant(int id, float x, bool facingRight, bool hasHurtbox = false)
        {
            Snapshot = new PhysicsParticipantSnapshot(id, $"p{id}", x, 0,
                DirectionValue.Neutral, facingRight,
                hasHurtbox
                    ? new[] { new CollisionBoxDefinition { BoxId = "body", Width = 20, Height = 40 } }
                    : Array.Empty<CollisionBoxDefinition>());
            Motion = new PhysicsMotionSnapshot(0, 0, false, 0);
        }

        public int PlayerId => Snapshot.PlayerId;
        public PhysicsParticipantSnapshot Snapshot { get; private set; }
        public PhysicsMotionSnapshot Motion { get; private set; }
        public PhysicsParticipantSnapshot CapturePhysicsSnapshot() => Snapshot;
        public PhysicsMotionSnapshot CaptureMotionSnapshot() => Motion;
        public void ApplyPhysicsState(PhysicsParticipantSnapshot participant, PhysicsMotionSnapshot motion)
        {
            Snapshot = participant;
            Motion = motion;
        }

        public void RestoreRuntimeSnapshot(PhysicsParticipantSnapshot participant, PhysicsMotionSnapshot motion)
        {
            Snapshot = participant;
            Motion = motion;
        }
    }

    private sealed class ProbeParticipant : IStateSnapshotParticipant
    {
        public ProbeParticipant(string discriminator, string value)
        {
            Discriminator = discriminator;
            _slot = new SnapshotReference<string>(value);
        }

        private readonly SnapshotReference<string> _slot;
        public string Discriminator { get; }
        public int CodecVersion => 1;
        public SnapshotComponent Capture(int frame, ulong epoch) => new(Discriminator, 1,
            System.Text.Json.JsonSerializer.Serialize(new ProbeDto(_slot.Value)));
        public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
        {
            var decoded = System.Text.Json.JsonSerializer.Deserialize<ProbeDto>(component.Payload)
                ?? throw new SnapshotPrepareException(Discriminator, "null");
            return PreparedSnapshotComponent.Create(Discriminator, _slot, decoded.Value);
        }

        private sealed record ProbeDto(string Value);
    }
}
