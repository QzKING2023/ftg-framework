#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Xunit.Abstractions;

namespace FTG_Framework.Tests;

/// <summary>Flowback regression for the interactive-acceptance finding that the
/// E4.1-REF-001 C-paths were unreachable: only 5LP had collision frames, 236P
/// had no input registration, and 5HP had no cancel window. This test runs the
/// real ryu data through the real input/physics pipeline and verifies every
/// reference path executes with its expected damage, hits, and terminal move.</summary>
[Collection(EventBusTestCollection.Name)]
public sealed class BalanceReferencePathTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-refpath-{Guid.NewGuid():N}");

    public BalanceReferencePathTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        _eventBusScope.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private const int Window = 200;

    private static string DataRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Scripts/Framework/Data"));

    private static DataStore RealData() => new(
        MoveDataLoader.LoadFromJson(File.ReadAllText(Path.Combine(DataRoot, "example_moves.json"))),
        GatlingDataLoader.LoadFromJson(File.ReadAllText(Path.Combine(DataRoot, "example_gatling.json"))),
        PhysicsDataLoader.LoadKnockbackProfilesFromJson(File.ReadAllText(Path.Combine(DataRoot, "example_knockback_profiles.json"))),
        PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(File.ReadAllText(Path.Combine(DataRoot, "example_physics_response_profiles.json"))));

    private sealed record PathExpectation(string[] Moves, int Damage, int Hits, string Terminal, int Duration);

    private static PathExpectation Expectation(string pathId) => pathId switch
    {
        "C1" => new(new[] { "5LP" }, 30, 1, "5LP", 11),
        "C2" => new(new[] { "5LP", "5HP" }, 110, 2, "5HP", 37),
        "C3" => new(new[] { "5LP", "5HP", "236P" }, 180, 3, "236P", 52),
        "C4" => new(new[] { "5LP", "5HP", "236P", "dp_c" }, 280, 4, "dp_c", 68),
        "C5" => new(new[] { "5LP", "5HP", "236P", "dp_d" }, 280, 4, "dp_d", 68),
        "C6" => new(new[] { "5LP", "5HP", "dp_c" }, 210, 3, "dp_c", 58),
        "C7" => new(new[] { "5LP", "5LP", "5HP" }, 140, 3, "5HP", 47),
        "C8" => new(new[] { "5LP", "5LP", "5HP", "236P", "dp_c" }, 310, 5, "dp_c", 78),
        _ => throw new ArgumentOutOfRangeException(nameof(pathId))
    };

    /// <summary>Press schedule per path: sources cancel one frame after their
    /// first collision frame so every move lands its hit, then the next move
    /// starts (5LP→5LP in C7/C8 relies on 5LP finishing naturally — the gatling
    /// table has no 5LP→5LP entry, so a cancel cannot be the mechanism).</summary>
    private static TrainingInputRecording PathRecording(string pathId)
    {
        var motion = new Dictionary<int, DirectionValue>();
        var buttons = new Dictionary<int, ButtonValue>();
        switch (pathId)
        {
            case "C1":
                buttons[0] = ButtonValue.A;
                break;
            case "C2":
                buttons[0] = ButtonValue.A;
                buttons[6] = ButtonValue.B;
                break;
            case "C3":
                buttons[0] = ButtonValue.A;
                buttons[6] = ButtonValue.B;
                motion[15] = DirectionValue.Down;
                motion[16] = DirectionValue.DownForward;
                motion[17] = DirectionValue.Forward;
                buttons[18] = ButtonValue.B;
                break;
            case "C4":
                buttons[0] = ButtonValue.A;
                buttons[6] = ButtonValue.B;
                motion[15] = DirectionValue.Down;
                motion[16] = DirectionValue.DownForward;
                motion[17] = DirectionValue.Forward;
                buttons[18] = ButtonValue.B;
                motion[27] = DirectionValue.Forward;
                motion[28] = DirectionValue.Down;
                motion[29] = DirectionValue.DownForward;
                buttons[30] = ButtonValue.C;
                break;
            case "C5":
                buttons[0] = ButtonValue.A;
                buttons[6] = ButtonValue.B;
                motion[15] = DirectionValue.Down;
                motion[16] = DirectionValue.DownForward;
                motion[17] = DirectionValue.Forward;
                buttons[18] = ButtonValue.B;
                motion[27] = DirectionValue.Forward;
                motion[28] = DirectionValue.Down;
                motion[29] = DirectionValue.DownForward;
                buttons[30] = ButtonValue.D;
                break;
            case "C6":
                buttons[0] = ButtonValue.A;
                buttons[6] = ButtonValue.B;
                motion[15] = DirectionValue.Forward;
                motion[16] = DirectionValue.Down;
                motion[17] = DirectionValue.DownForward;
                buttons[18] = ButtonValue.C;
                break;
            case "C7":
                buttons[0] = ButtonValue.A;
                buttons[10] = ButtonValue.A;
                buttons[16] = ButtonValue.B;
                break;
            case "C8":
                buttons[0] = ButtonValue.A;
                buttons[10] = ButtonValue.A;
                buttons[16] = ButtonValue.B;
                motion[25] = DirectionValue.Down;
                motion[26] = DirectionValue.DownForward;
                motion[27] = DirectionValue.Forward;
                buttons[28] = ButtonValue.B;
                motion[37] = DirectionValue.Forward;
                motion[38] = DirectionValue.Down;
                motion[39] = DirectionValue.DownForward;
                buttons[40] = ButtonValue.C;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pathId));
        }

        const int duration = 90;
        var entries = new List<TrainingInputRecordingEntry>();
        for (int frame = 0; frame <= duration; frame++)
        {
            DirectionValue direction = motion.TryGetValue(frame, out var d) ? d : DirectionValue.Neutral;
            entries.Add(new TrainingInputRecordingEntry(frame, 0, InputType.Directional, (int)direction));
            if (buttons.TryGetValue(frame, out var button))
                entries.Add(new TrainingInputRecordingEntry(frame, 1, InputType.Button, (int)button));
        }
        return new TrainingInputRecording(1, 1, duration, pathId, entries);
    }

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
            TrainingInput.Shutdown();
        }
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

    private TrialHarness Harness()
    {
        var data = RealData();
        var stateMachine = new StateMachine(data);
        var inputHistory = new InputHistory(600);
        var chargeTracker = new ChargeTracker(inputHistory);
        var leniency = new InputLeniencyMatcher(inputHistory);
        GameLoop.RegisterDefaultMoves(leniency);
        var inputBuffer = new InputBuffer(inputHistory, leniency, bufferDuration: 6, motionWindow: 30);
        var priorityResolver = new DefaultPriorityResolver(leniency, chargeTracker);
        var frameData = new FrameDataEngine(data);
        var physics = new PhysicsEngine(data, frameData, stateMachine);
        stateMachine.Initialize(data);
        stateMachine.RegisterStateProfile(CharacterState.Idle, "default");
        stateMachine.RegisterStateProfile(CharacterState.Hitstun, "default");
        stateMachine.RegisterStateProfile(CharacterState.Blockstun, "default");
        stateMachine.RegisterStateProfile(CharacterState.Airborne, "default");
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
            (player, fromFrame) =>
            {
                inputHistory.ResetTrainingTransient(player, fromFrame);
                chargeTracker.ResetPlayer(player);
            },
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

    private static BalanceTrialService Trial(TrialHarness h, string snapshotPath) => new(
        h.SaveService, h.TrainingInput, h.Library, h.PlaybackModes, h.Data,
        currentFrame: () => EventBus.Instance.CurrentFrame,
        currentEpoch: () => EventBus.Instance.LifecycleEpoch,
        versions: () => new BalanceDataVersions(h.Data.MoveDatasetVersion, h.Data.PhysicsDatasetVersion),
        frameHash: (frame, targetPlayer) => BalanceFrameHash.Compute(frame, targetPlayer, h.FrameData, h.StateMachine, h.Physics, h.Combo, h.InputHistory, h.ChargeTracker),
        playerPositionX: player => (int)h.Physics.CaptureRuntimeSnapshot().Participants[player].WorldX,
        playerTerminalMoveId: player => h.FrameData.GetCurrentMoveId(player));

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

    private static int ParseTraceFrame(string entry)
    {
        if (!entry.StartsWith("f", StringComparison.Ordinal))
            return 0;
        int frameEnd = entry.IndexOf(':');
        return frameEnd > 1 && int.TryParse(entry[1..frameEnd], out int frame) ? frame : 0;
    }

    private static int MoveTotal(TrialHarness h, string moveId)
    {
        var move = h.Data.GetMove(moveId);
        return move is null ? 0 : move.Startup + move.Active + move.Recovery;
    }

    [Theory]
    [InlineData("C1")]
    [InlineData("C2")]
    [InlineData("C3")]
    [InlineData("C4")]
    [InlineData("C5")]
    [InlineData("C6")]
    [InlineData("C7")]
    [InlineData("C8")]
    public void ReferencePath_ExecutesWithExpectedOutcomes(string pathId)
    {
        TrialHarness h = Harness();
        string snapshot = Path.Combine(_directory, $"{pathId}.json");
        int hits = 0;
        try
        {
            var recording = PathRecording(pathId);
            Assert.True(h.Library.TryAddOrReplace(recording, false, null, out string libError), libError);
            Assert.True(h.SaveService.TrySave(snapshot, out string saveError), saveError);

            var trial = Trial(h, snapshot);
            Assert.True(trial.TryPrepare(new BalanceTrialCandidate(snapshot, pathId, 1, Window, ReferenceDatasetId: "E4.1-REF-001"), out string prepareError), prepareError);
            Assert.Equal("E4.1-REF-001", trial.Binding!.ReferenceDatasetId);
            Assert.True(trial.TryStart(out string startError), startError);
            var hitHandler = new Action<HitConnectedEvent>(evt => { if (evt.AttackerId == 1) hits++; });
            EventBus.Instance.Subscribe(hitHandler);
            var trace = new List<string>();
            var onMoveStarted = new Action<MoveStartedEvent>(evt => trace.Add($"f{EventBus.Instance.CurrentFrame}:start {evt.MoveId}"));
            var onMoveCanceled = new Action<MoveCanceledEvent>(evt => trace.Add($"f{EventBus.Instance.CurrentFrame}:cancel {evt.FromMove}->{evt.ToMove}"));
            var onTraceHit = new Action<HitConnectedEvent>(evt => trace.Add($"f{EventBus.Instance.CurrentFrame}:hit {evt.MoveId} dmg={evt.Damage}"));
            EventBus.Instance.Subscribe(onMoveStarted);
            EventBus.Instance.Subscribe(onMoveCanceled);
            EventBus.Instance.Subscribe(onTraceHit);
            try
            {
                RunFrames(h, Window + 6);
            }
            finally
            {
                EventBus.Instance.Unsubscribe(onTraceHit);
                EventBus.Instance.Unsubscribe(onMoveCanceled);
                EventBus.Instance.Unsubscribe(onMoveStarted);
                EventBus.Instance.Unsubscribe(hitHandler);
            }
            _output.WriteLine($"[TRACE {pathId}] " + string.Join(" | ", trace));

            Assert.Equal(BalanceTrialState.Completed, trial.State);
            BalanceTrialResult result = trial.Result!;
            PathExpectation expected = Expectation(pathId);

            // The executed sequence is reconstructed from start/cancel events:
            // cancels replace the current move without publishing MoveStarted,
            // so initiations alone undercount chains (a trial-metric design note).
            var executed = new List<(int Frame, string Move)>();
            foreach (string entry in trace)
            {
                int frame = ParseTraceFrame(entry);
                if (entry.Contains(":start "))
                    executed.Add((frame, entry[(entry.IndexOf(":start ") + 7)..]));
                else if (entry.Contains(":cancel "))
                    executed.Add((frame, entry[(entry.IndexOf("->") + 2)..]));
            }
            Assert.NotEmpty(executed);
            string terminal = executed[^1].Move;
            int duration = executed[^1].Frame + MoveTotal(h, terminal);

            _output.WriteLine(
                $"[C] {pathId}: damage={result.TotalDamage} hits={hits} maxCombo={result.MaxComboHits} " +
                $"terminal={result.TerminalMoveId ?? "<none>"} executed=[{string.Join(",", executed.Select(e => e.Move))}] duration={duration}");

            Assert.Equal(expected.Moves, executed.Select(e => e.Move).ToArray());
            Assert.Equal(expected.Damage, result.TotalDamage);
            Assert.Equal(expected.Hits, hits);
            Assert.Equal(expected.Terminal, result.TerminalMoveId);
            Assert.Equal(expected.Duration, duration);
        }
        finally
        {
            h.Shutdown();
        }
    }
}
