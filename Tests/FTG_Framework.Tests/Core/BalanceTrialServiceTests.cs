#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public sealed class BalanceTrialServiceTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-trial-{Guid.NewGuid():N}");

    public BalanceTrialServiceTests() => Directory.CreateDirectory(_directory);
    public void Dispose()
    {
        _eventBusScope.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private const int Window = 40;

    private sealed record TrialHarness(
        DataStore Data,
        StateMachine StateMachine,
        FrameDataEngine FrameData,
        PhysicsEngine Physics,
        InputHistory InputHistory,
        ChargeTracker ChargeTracker,
        InputLeniencyMatcher Leniency,
        InputBuffer InputBuffer,
        DefaultPriorityResolver PriorityResolver,
        ComboStateTracker Combo,
        ComboExecutor ComboExecutor,
        TrainingInputService TrainingInput,
        TrainingInputRecordingLibrary Library,
        StateSnapshotCoordinator Coordinator,
        ReplayOrchestrator Orchestrator,
        TrainingStateService SaveService,
        PlaybackModeCoordinator PlaybackModes)
    {
        public void Shutdown()
        {
            Physics.Shutdown();
            FrameData.Shutdown();
            ChargeTracker.Shutdown();
            InputHistory.Shutdown();
            StateMachine.Shutdown();
            Combo.Shutdown();
            ComboExecutor.Shutdown();
            Leniency.Shutdown();
            InputBuffer.Shutdown();
            PriorityResolver.Shutdown();
        }
    }

    private static MoveDefinition FiveA(int damage = 10) => new()
    {
        MoveId = "5A",
        Startup = 1,
        Active = 1,
        Recovery = 1,
        HitAdvantage = 5,
        BlockAdvantage = -2,
        Damage = damage,
        ChainRepeatable = false,
        KnockbackProfileId = "launch",
        CollisionFrames = new[]
        {
            new CollisionFrameDefinition
            {
                Frame = 2,
                Hitboxes = new[] { new CollisionBoxDefinition { BoxId = "hit-a", X = 10, Width = 300, Height = 20 } }
            }
        }
    };

    private static TrainingInputRecording TrialRecording()
    {
        // Live neutral is captured every frame; the input buffer resolves a
        // neutral normal only when its direction and button both sit on the
        // current frame, so the recording must mirror that cadence.
        var entries = new List<TrainingInputRecordingEntry>();
        for (int frame = 0; frame <= 30; frame++)
        {
            entries.Add(new TrainingInputRecordingEntry(frame, 0, InputType.Directional, (int)DirectionValue.Neutral));
            if (frame is 0 or 5 or 10)
                entries.Add(new TrainingInputRecordingEntry(frame, 1, InputType.Button, (int)ButtonValue.A));
        }
        return new TrainingInputRecording(1, 1, 30, "rtrial", entries);
    }

    private TrialHarness Harness()
    {
        var data = new DataStore(new[] { FiveA() },
            knockbackProfiles: new[]
            {
                new KnockbackProfile { ProfileId = "launch", Horizontal = 8, Friction = 0.3f }
            });
        var stateMachine = new StateMachine(data);
        var inputHistory = new InputHistory(600);
        var chargeTracker = new ChargeTracker(inputHistory);
        var leniency = new InputLeniencyMatcher(inputHistory);
        leniency.RegisterMove(new MoveInputConfig
        {
            MoveId = "5A",
            AcceptedSequences = new[] { new[] { DirectionValue.Neutral } },
            RequiredButton = ButtonValue.A,
            Category = MoveCategory.Normal
        });
        var inputBuffer = new InputBuffer(inputHistory, leniency, bufferDuration: 6, motionWindow: 30);
        var priorityResolver = new DefaultPriorityResolver(leniency, chargeTracker);
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
        var comboExecutor = new ComboExecutor(data, frameData);
        comboExecutor.Initialize(data);
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
        return new TrialHarness(data, stateMachine, frameData, physics, inputHistory, chargeTracker,
            leniency, inputBuffer, priorityResolver, combo, comboExecutor, trainingInput, library,
            coordinator, orchestrator, saveService, playbackModes);
    }

    private static BalanceTrialService Trial(TrialHarness h, string snapshotPath)
    {
        return new BalanceTrialService(
            h.SaveService, h.TrainingInput, h.Library, h.PlaybackModes, h.Data,
            currentFrame: () => EventBus.Instance.CurrentFrame,
            currentEpoch: () => EventBus.Instance.LifecycleEpoch,
            versions: () => new BalanceDataVersions(h.Data.MoveDatasetVersion, h.Data.PhysicsDatasetVersion),
            frameHash: (frame, targetPlayer) => BalanceFrameHash.Compute(frame, targetPlayer, h.FrameData, h.StateMachine, h.Physics, h.Combo, h.InputHistory, h.ChargeTracker),
            playerPositionX: player => (int)h.Physics.CaptureRuntimeSnapshot().Participants[player].WorldX,
            playerTerminalMoveId: player => h.FrameData.GetCurrentMoveId(player));
    }

    private static string SaveInitialSnapshot(TrialHarness h, string path)
    {
        var recording = TrialRecording();
        Assert.True(h.Library.TryAddOrReplace(recording, false, null, out string libraryError), libraryError);
        Assert.True(h.SaveService.TrySave(path, out string saveError), saveError);
        return path;
    }

    private static void RunFrames(TrialHarness h, int frames)
    {
        ulong epoch = EventBus.Instance.LifecycleEpoch;
        for (int frame = 0; frame < frames; frame++)
        {
            h.TrainingInput.ProcessPlaybackFrame(EventBus.Instance.CurrentFrame, epoch);
            var matches = h.InputBuffer.TryMatch(1);
            var resolved = h.PriorityResolver.Resolve(matches ?? Array.Empty<MatchResult>(), 1, EventBus.Instance.CurrentFrame);
            if (resolved is { } match)
            {
                if (h.FrameData.GetPhase(1) == MovePhase.Idle)
                    h.FrameData.StartMove(1, match.MoveId);
                else
                    h.ComboExecutor.TryCancel(1, match.MoveId);
            }
            h.ChargeTracker.Update(1, EventBus.Instance.CurrentFrame);
            h.ChargeTracker.Update(2, EventBus.Instance.CurrentFrame);
            h.FrameData.Update();
            h.Physics.Update();
            h.TrainingInput.CompleteCaptureFrame(EventBus.Instance.CurrentFrame);
            EventBus.Instance.ProcessFrame();
            h.TrainingInput.CompleteFrame();
        }
    }

    private static BalanceTrialResult RunTrial(TrialHarness h, BalanceTrialService trial, string snapshotPath)
    {
        Assert.True(trial.TryPrepare(new BalanceTrialCandidate(snapshotPath, "rtrial", 1, Window), out string prepareError), prepareError);
        Assert.True(trial.TryStart(out string startError), startError);
        RunFrames(h, Window + 6);
        Assert.Equal(BalanceTrialState.Completed, trial.State);
        return trial.Result!;
    }

    [Fact]
    public void Prepare_InvalidCandidate_RejectsCompleteTrialWithoutMutation()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "valid.json"));
        var trial = Trial(h, path);
        ulong epoch = EventBus.Instance.LifecycleEpoch;
        try
        {
            var invalid = new (BalanceTrialCandidate Candidate, string Fragment)[]
            {
                (new BalanceTrialCandidate(path, "rtrial", 1, 0), "at least one frame"),
                (new BalanceTrialCandidate(path, "rtrial", 3, Window), "player must be 1 or 2"),
                (new BalanceTrialCandidate(path, "", 1, Window), "valid recording name"),
                (new BalanceTrialCandidate(Path.Combine(_directory, "missing.json"), "rtrial", 1, Window), "must exist"),
                (new BalanceTrialCandidate(path, "rtrial", 1, Window, ExpectedSnapshotSha256: "0000000000000000000000000000000000000000000000000000000000000000"), "identity mismatch"),
                (new BalanceTrialCandidate(path, "nosuch", 1, Window), "does not exist")
            };
            foreach ((BalanceTrialCandidate candidate, string fragment) in invalid)
            {
                Assert.False(trial.TryPrepare(candidate, out string error), $"expected rejection: {candidate}");
                Assert.Contains(fragment, error, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(BalanceTrialState.Idle, trial.State);
                Assert.Equal(epoch, EventBus.Instance.LifecycleEpoch);
                Assert.False(h.TrainingInput.IsPlaying, "rejected prepare must not start playback");
                Assert.Null(h.FrameData.GetCurrentMoveId(1));
            }
        }
        finally { h.Shutdown(); }
    }

    [Fact]
    public void Prepare_AuthoritativeReplayActive_RejectsWithOwnershipConflict()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "replay.json"));
        var trial = Trial(h, path);
        try
        {
            h.PlaybackModes.Enter(RuntimePlaybackMode.AuthoritativeReplay, EventBus.Instance.LifecycleEpoch);
            Assert.False(trial.TryPrepare(new BalanceTrialCandidate(path, "rtrial", 1, Window), out string error));
            Assert.Contains("replay", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(BalanceTrialState.Idle, trial.State);
        }
        finally
        {
            h.PlaybackModes.Exit(RuntimePlaybackMode.AuthoritativeReplay);
            h.Shutdown();
        }
    }

    [Fact]
    public void Prepare_DanglingSnapshotMoveReferenceInCurrentDataset_Rejects()
    {
        TrialHarness h = Harness();
        string path = Path.Combine(_directory, "dangling.json");
        try
        {
            h.FrameData.StartMove(1, "5A");
            h.FrameData.Update();
            Assert.True(h.SaveService.TrySave(path, out string saveError), saveError);
            Assert.True(h.Data.TryCommitMoveDataset(new[] { new MoveDefinition { MoveId = "6A", Startup = 1, Active = 1, Recovery = 1 } },
                h.Data.MoveDatasetVersion, () => true));

            var trial = Trial(h, path);
            Assert.False(trial.TryPrepare(new BalanceTrialCandidate(path, "rtrial", 1, Window), out string error));
            Assert.Contains("dangling reference", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(BalanceTrialState.Idle, trial.State);
        }
        finally { h.Shutdown(); }
    }

    [Theory]
    [InlineData(2202)]
    [InlineData(2203)]
    [InlineData(2204)]
    [InlineData(2205)]
    [InlineData(2206)]
    public void StartAndRun_ProducesMetricsAndHashTrail_DeterministicAcrossRepeatedTrials(int seed)
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, $"run-{seed}.json"));
        var trial = Trial(h, path);
        try
        {
            BalanceTrialResult first = RunTrial(h, trial, path);
            Assert.Equal(Window, first.HashedFrames);
            Assert.True(first.TotalDamage > 0, "scenario must land hits");
            Assert.True(first.MaxComboHits >= 1, "scenario must complete at least one combo");
            Assert.Equal("5A", first.TerminalMoveId);
            Assert.NotEmpty(first.Initiations);

            // Repeated identical trial (AC06): same snapshot, recording, versions.
            BalanceTrialResult second = RunTrial(h, trial, path);
            Assert.Equal(first.HashTrail, second.HashTrail);
            Assert.Equal(first.TotalDamage, second.TotalDamage);
            Assert.Equal(first.MaxComboHits, second.MaxComboHits);
            Assert.Equal(first.TerminalPositionX, second.TerminalPositionX);
            Assert.Equal(first.Initiations.Select(i => (i.Frame, i.MoveId, i.Versions)),
                second.Initiations.Select(i => (i.Frame, i.MoveId, i.Versions)));
        }
        finally { h.Shutdown(); }
    }

    [Fact]
    public void ABComparison_TuningVersionChange_AttributesDifferencesToCommittedVersions()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "ab.json"));
        var trial = Trial(h, path);
        var viewModel = new FTG_Framework.UI.Training.ViewModels.BalanceTrialComparisonViewModel();
        try
        {
            BalanceTrialResult trialA = RunTrial(h, trial, path);

            // Commit tuning version B: damage 10 -> 12.
            var modified = h.Data.GetAllMoves()
                .Select(m => m.MoveId == "5A" ? FiveA(damage: 12) : m)
                .ToArray();
            Assert.True(h.Data.TryCommitMoveDataset(modified, h.Data.MoveDatasetVersion, () => true));

            BalanceTrialResult trialB = RunTrial(h, trial, path);

            Assert.True(trialA.Binding.Versions.MoveDatasetVersion < trialB.Binding.Versions.MoveDatasetVersion,
                "trial B must bind the newer committed data version");
            Assert.True(trialA.TotalDamage != trialB.TotalDamage,
                "tuned damage must be observable in the comparable metric");

            viewModel.AddResult(trialA);
            viewModel.AddResult(trialB);
            viewModel.SelectLeft(trialA);
            viewModel.SelectRight(trialB);
            var comparison = viewModel.Compare();

            Assert.NotNull(comparison);
            var damageRow = Assert.Single(comparison!.Rows, row => row.Metric == "Total damage");
            Assert.False(damageRow.Equal);
            var versionRow = Assert.Single(comparison.Rows, row => row.Metric == "Move dataset version");
            Assert.False(versionRow.Equal, "version row must attribute the difference to committed data versions");
        }
        finally { h.Shutdown(); }
    }

    [Fact]
    public void MidTrialTuningCommit_RecordsInitiationVersionsAtBoundary()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "versions.json"));
        var trial = Trial(h, path);
        try
        {
            Assert.True(trial.TryPrepare(new BalanceTrialCandidate(path, "rtrial", 1, Window), out string prepareError), prepareError);
            Assert.True(trial.TryStart(out string startError), startError);
            ulong versionBeforeCommit = h.Data.MoveDatasetVersion;

            RunFrames(h, 3);
            Assert.True(h.Data.TryCommitMoveDataset(h.Data.GetAllMoves().ToArray(), h.Data.MoveDatasetVersion, () => true),
                "mid-trial committed-version change");
            ulong versionAfterCommit = h.Data.MoveDatasetVersion;
            Assert.True(versionAfterCommit > versionBeforeCommit);
            RunFrames(h, Window + 3);

            Assert.Equal(BalanceTrialState.Completed, trial.State);
            var initiations = trial.Result!.Initiations;
            Assert.True(initiations.Count >= 2,
                $"scenario must initiate several moves: {string.Join(";", initiations.Select(i => $"{i.Frame}:{i.MoveId}:{i.Versions.MoveDatasetVersion}"))}");
            Assert.Equal(versionBeforeCommit, initiations[0].Versions.MoveDatasetVersion);
            Assert.True(initiations.Skip(1).All(i => i.Versions.MoveDatasetVersion == versionAfterCommit),
                "initiations after the commit boundary must record the new committed version");
        }
        finally { h.Shutdown(); }
    }

    [Fact]
    public void MidTrialLifecycleTransition_InvalidatesExactlyOnce_NoStaleMetrics()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "lifecycle.json"));
        var trial = Trial(h, path);
        try
        {
            Assert.True(trial.TryPrepare(new BalanceTrialCandidate(path, "rtrial", 1, Window), out string prepareError), prepareError);
            Assert.True(trial.TryStart(out string startError), startError);
            RunFrames(h, 3);

            EventBus.Instance.PublishImmediate(new MatchInitializedEvent("p1", "p2"));

            Assert.Equal(BalanceTrialState.Invalidated, trial.State);
            Assert.Null(trial.Result);
            Assert.Contains("Match", trial.CancellationReason, StringComparison.Ordinal);
            Assert.False(h.TrainingInput.IsPlaying, "invalidation must release playback before cleanup");

            RunFrames(h, 10);
            Assert.Equal(BalanceTrialState.Invalidated, trial.State);
            Assert.Null(trial.Result);

            // Exactly-once: a second lifecycle transition must not re-invalidate.
            EventBus.Instance.PublishImmediate(new MatchInitializedEvent("p1", "p2"));
            Assert.Equal(BalanceTrialState.Invalidated, trial.State);
            Assert.StartsWith("[Trial] Match initialized.", trial.CancellationReason);
        }
        finally { h.Shutdown(); }
    }

    [Fact]
    public void StartFailure_BeforeCommittedSwap_AbortsWithoutMutation()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "fail.json"));
        var trial = Trial(h, path);
        try
        {
            Assert.True(trial.TryPrepare(new BalanceTrialCandidate(path, "rtrial", 1, Window), out string prepareError), prepareError);
            ulong epoch = EventBus.Instance.LifecycleEpoch;
            File.WriteAllText(path, "corrupted candidate");

            Assert.False(trial.TryStart(out string startError));
            Assert.Contains("Restore failed", startError);
            Assert.Equal(BalanceTrialState.Idle, trial.State);
            Assert.Equal(epoch, EventBus.Instance.LifecycleEpoch);
            Assert.False(h.TrainingInput.IsPlaying);
            Assert.Null(trial.Result);
            Assert.Null(h.FrameData.GetCurrentMoveId(1));
        }
        finally { h.Shutdown(); }
    }

    [Fact]
    public void Start_IsExactlyOnce()
    {
        TrialHarness h = Harness();
        string path = SaveInitialSnapshot(h, Path.Combine(_directory, "once.json"));
        var trial = Trial(h, path);
        try
        {
            Assert.True(trial.TryPrepare(new BalanceTrialCandidate(path, "rtrial", 1, Window), out string prepareError), prepareError);
            Assert.True(trial.TryStart(out string startError), startError);
            Assert.False(trial.TryStart(out string secondError));
            Assert.Contains("exactly-once", secondError);
            trial.Cancel();
            Assert.Equal(BalanceTrialState.Invalidated, trial.State);
        }
        finally { h.Shutdown(); }
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
}
