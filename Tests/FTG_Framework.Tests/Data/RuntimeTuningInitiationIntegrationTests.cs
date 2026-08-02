#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Engine.Physics;
using StateMachineImpl = FTG_Framework.Engine.StateMachine.StateMachine;
using Xunit;

namespace FTG_Framework.Tests.DataLayer;

[Collection(EventBusTestCollection.Name)]
public sealed class RuntimeTuningInitiationIntegrationTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(
        EventBusResidualState.Subscribers | EventBusResidualState.Queues);
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ftg-tuning-initiation-{Guid.NewGuid():N}");

    public RuntimeTuningInitiationIntegrationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void MoveWriteback_CurrentComboKeepsRepeatabilitySnapshot_NextComboUsesCommit()
    {
        const string movesJson = """
            {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":1,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[{"start_frame":0,"end_frame":2,"target_category":"normal"}],"collision_frames":[{"frame":2,"hitboxes":[{"box_id":"hit-a","x":10,"y":0,"width":20,"height":20}],"hurtboxes":[]}]},{"move_id":"5B","startup":1,"active":1,"recovery":1,"hit_advantage":1,"block_advantage":0,"damage":12,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
            """;
        const string knockbackJson = """
            {"schema_version":1,"knockback_profiles":[{"profile_id":"light","horizontal":1,"vertical":2,"gravity":3,"friction":4}]}
            """;
        const string responseJson = """
            {"schema_version":1,"physics_response_profiles":[{"profile_id":"default","knockback_multiplier":1,"gravity_scale":1,"friction":0.5,"air_friction":0.2,"participates_in_hitstop":true}]}
            """;
        Write("moves.json", movesJson);
        Write("knockback.json", knockbackJson);
        Write("response.json", responseJson);
        var table = new GatlingTable
        {
            CharacterId = "ryu",
            Entries = [new GatlingEntry { SourceMove = "5A", TargetMoves = ["5B"], CancelCategory = "normal" }]
        };
        var store = new DataStore(MoveDataLoader.LoadFromJson(movesJson), [table],
            PhysicsDataLoader.LoadKnockbackProfilesFromJson(knockbackJson),
            PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(responseJson));
        var service = Service(store);
        var executor = new ComboExecutor(store, new StubFrames());
        executor.Initialize(store);
        try
        {
            EventBus.Instance.PublishImmediate(new MoveFrameChangedEvent(1, "5B", 0, 3, MovePhase.Startup));
            EventBus.Instance.PublishImmediate(new CancelWindowEnteredEvent(1, "5A", "normal", 0, 99));

            RuntimeTuningBaseline baseline = service.Load(new(RuntimeTuningSelectionKind.Move, "moves", "5B"));
            var fields = new Dictionary<string, string>(baseline.Fields) { ["chain_repeatable"] = "true" };
            Assert.True(service.Commit(new RuntimeTuningCommitRequest(
                baseline.Selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, 1)).Committed);

            Assert.False(executor.TryCancel(1, "5B"));

            EventBus.Instance.PublishImmediate(new ComboEndedEvent(1, 1, "reset"));
            EventBus.Instance.PublishImmediate(new MoveFrameChangedEvent(1, "5B", 0, 3, MovePhase.Startup));
            EventBus.Instance.PublishImmediate(new CancelWindowEnteredEvent(1, "5A", "normal", 0, 99));
            Assert.True(executor.TryCancel(1, "5B"));
        }
        finally { executor.Shutdown(); }
    }

    [Fact]
    public void MoveWriteback_ActiveTimelineKeepsDefinition_NextStartUsesCommit()
    {
        var (service, store) = CreateStandardService();
        var engine = new FrameDataEngine(store);
        engine.Initialize(store);
        try
        {
            engine.StartMove(1, "5A");
            RuntimeTuningBaseline baseline = service.Load(new(RuntimeTuningSelectionKind.Move, "moves", "5A"));
            var fields = new Dictionary<string, string>(baseline.Fields) { ["startup"] = "2" };
            Assert.True(service.Commit(new RuntimeTuningCommitRequest(
                baseline.Selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, 1)).Committed);

            MoveFrameChangedEvent current = Assert.Single(EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
            {
                engine.Update();
                EventBus.Instance.ProcessFrame();
            }));
            Assert.Equal(3, current.TotalFrames);

            engine.Update();
            engine.Update();
            EventBus.Instance.ProcessFrame();
            Assert.Equal(MovePhase.Idle, engine.GetPhase(1));
            engine.StartMove(1, "5A");
            MoveFrameChangedEvent next = Assert.Single(EventBusTestHelper.Collect<MoveFrameChangedEvent>(() =>
            {
                engine.Update();
                EventBus.Instance.ProcessFrame();
            }));
            Assert.Equal(4, next.TotalFrames);
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void KnockbackWriteback_ExistingHitContextKeepsProfile_NextContactUsesCommit()
    {
        var (service, store) = CreateStandardService();
        var frames = new StubFrames { P1 = new EvaluatedMoveFrame("5A", 1, 1, MovePhase.Active) };
        var engine = new PhysicsEngine(store, frames);
        engine.Initialize(store);
        engine.Register(new StubParticipant(1, -5, true));
        engine.Register(new StubParticipant(2, 5, false));

        int firstFrame = EventBus.Instance.CurrentFrame;
        engine.Update();
        EventBus.Instance.ProcessFrame();
        Assert.True(engine.TryGetHitContext(1, 2, "hit-a", firstFrame, out HitContext first));
        Assert.Equal(1, first.KnockbackProfile!.Horizontal);

        RuntimeTuningBaseline baseline = service.Load(new(RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light"));
        var fields = new Dictionary<string, string>(baseline.Fields) { ["horizontal"] = "9" };
        Assert.True(service.Commit(new RuntimeTuningCommitRequest(
            baseline.Selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, 1)).Committed);
        Assert.Equal(1, first.KnockbackProfile.Horizontal);

        frames.P1 = EvaluatedMoveFrame.Idle;
        engine.Update();
        EventBus.Instance.ProcessFrame();
        frames.P1 = new EvaluatedMoveFrame("5A", 2, 1, MovePhase.Active);
        int nextFrame = EventBus.Instance.CurrentFrame;
        engine.Update();
        EventBus.Instance.ProcessFrame();
        Assert.True(engine.TryGetHitContext(1, 2, "hit-a", nextFrame, out HitContext next));
        Assert.Equal(9, next.KnockbackProfile!.Horizontal);
    }

    [Fact]
    public void ResponseWriteback_CurrentStateKeepsProfile_NextStateEntryUsesCommit()
    {
        var (service, store) = CreateStandardService();
        var stateMachine = new StateMachineImpl(store);
        stateMachine.Initialize(store);
        try
        {
            stateMachine.RegisterStateProfile(CharacterState.Idle, "default");
            stateMachine.RegisterStateProfile(CharacterState.Hitstun, "default");
            stateMachine.InitializePlayer(1);
            Assert.Equal(1, stateMachine.GetEffectivePhysicsProfile(1).KnockbackMultiplier);

            RuntimeTuningBaseline baseline = service.Load(new(RuntimeTuningSelectionKind.PhysicsResponseProfile, "response", "default"));
            var fields = new Dictionary<string, string>(baseline.Fields) { ["knockback_multiplier"] = "2" };
            Assert.True(service.Commit(new RuntimeTuningCommitRequest(
                baseline.Selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, 1)).Committed);
            Assert.Equal(1, stateMachine.GetEffectivePhysicsProfile(1).KnockbackMultiplier);

            stateMachine.ReplaceState(1, CharacterState.Hitstun);
            Assert.Equal(2, stateMachine.GetEffectivePhysicsProfile(1).KnockbackMultiplier);
        }
        finally { stateMachine.Shutdown(); }
    }

    private RuntimeTuningService Service(DataStore store) => new(_root, store,
        Path.Combine(_root, "knockback.json"), Path.Combine(_root, "response.json"), () => ["default"]);

    private (RuntimeTuningService Service, DataStore Store) CreateStandardService()
    {
        const string moves = """
            {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":1,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[{"frame":2,"hitboxes":[{"box_id":"hit-a","x":10,"y":0,"width":20,"height":20}],"hurtboxes":[]}]}]}
            """;
        const string knockbacks = """
            {"schema_version":1,"knockback_profiles":[{"profile_id":"light","horizontal":1,"vertical":2,"gravity":3,"friction":4}]}
            """;
        const string responses = """
            {"schema_version":1,"physics_response_profiles":[{"profile_id":"default","knockback_multiplier":1,"gravity_scale":1,"friction":0.5,"air_friction":0.2,"participates_in_hitstop":true}]}
            """;
        Write("moves.json", moves);
        Write("knockback.json", knockbacks);
        Write("response.json", responses);
        var store = new DataStore(MoveDataLoader.LoadFromJson(moves),
            knockbackProfiles: PhysicsDataLoader.LoadKnockbackProfilesFromJson(knockbacks),
            physicsResponseProfiles: PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(responses));
        return (Service(store), store);
    }

    private void Write(string name, string contents) =>
        File.WriteAllText(Path.Combine(_root, name), contents, new UTF8Encoding(false));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        _eventBusScope.Dispose();
    }

    private sealed class StubFrames : IFrameDataEngine
    {
        internal EvaluatedMoveFrame P1 = EvaluatedMoveFrame.Idle;
        public void StartMove(int playerId, string moveId) { }
        public void InterruptAndStart(int playerId, string moveId) { }
        public void Update() { }
        public MovePhase GetPhase(int playerId) => MovePhase.Startup;
        public int GetCurrentFrame(int playerId) => 0;
        public string? GetCurrentMoveId(int playerId) => "5A";
        public EvaluatedMoveFrame GetLastEvaluatedFrame(int playerId) => playerId == 1 ? P1 : EvaluatedMoveFrame.Idle;
        public bool RestoreFrame(int frameNumber) => false;
        public int EarliestSnapshotFrame => -1;
        public FrameStateSnapshot? TryGetSnapshot(int frameNumber) => null;
        public void RestoreFromReplaySnapshot(FrameStateSnapshot snapshot) { }
    }

    private sealed class StubParticipant : IPhysicsParticipant
    {
        private readonly PhysicsParticipantSnapshot _snapshot;
        internal StubParticipant(int playerId, float x, bool facingRight) =>
            _snapshot = new PhysicsParticipantSnapshot(playerId, $"p{playerId}", x, 0,
                DirectionValue.Neutral, facingRight,
                [new CollisionBoxDefinition { BoxId = "body", Width = 20, Height = 40 }]);
        public int PlayerId => _snapshot.PlayerId;
        public PhysicsParticipantSnapshot CapturePhysicsSnapshot() => _snapshot;
    }
}
