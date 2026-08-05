#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Engine.Physics;
using FTG_Framework.Engine.StateMachine;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Core;

[Collection(EventBusTestCollection.Name)]
public sealed class StateSnapshotCoordinatorTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);

    public void Dispose() => _eventBusScope.Dispose();

    [Fact]
    public void Restore_PrepareFailure_PreservesLiveStateEpochAndQueuedWork()
    {
        var first = new FakeParticipant("frame_data", "old-a");
        var failing = new FakeParticipant("physics", "old-b") { FailPrepare = true };
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [first, failing]);
        StateSnapshot snapshot = coordinator.Capture(frame: 4, frameworkVersion: "2.3.0");
        ulong epoch = EventBus.Instance.LifecycleEpoch;
        int observed = 0;
        Action<ReplayPausedEvent> paused = _ => observed++;
        EventBus.Instance.Subscribe(paused);
        EventBus.Instance.Publish(new ReplayPausedEvent(1));
        try
        {
            Assert.Throws<SnapshotPrepareException>(() => coordinator.Restore(snapshot, SnapshotRestoreMode.Normal));
            EventBus.Instance.ProcessFrame();
            Assert.Equal("old-a", first.LiveValue);
            Assert.Equal("old-b", failing.LiveValue);
            Assert.Equal(epoch, EventBus.Instance.LifecycleEpoch);
            Assert.Equal(1, observed);
        }
        finally { EventBus.Instance.Unsubscribe(paused); }
    }

    [Fact]
    public void Restore_Success_CommitsInOrderEmitsOnceAndResumesAtFramePlusOne()
    {
        var first = new FakeParticipant("frame_data", "captured-a");
        var second = new FakeParticipant("physics", "captured-b");
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [second, first]);
        StateSnapshot snapshot = coordinator.Capture(frame: 9, frameworkVersion: "2.3.0");
        first.LiveValue = "mutated-a";
        second.LiveValue = "mutated-b";
        int restored = 0;
        Action<StateRestoredEvent> handler = _ => restored++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            coordinator.Restore(snapshot, SnapshotRestoreMode.Normal);
            Assert.Equal(["frame_data", "physics"], coordinator.ParticipantOrder);
            Assert.Equal("captured-a", first.LiveValue);
            Assert.Equal("captured-b", second.LiveValue);
            Assert.Equal(1, restored);
            Assert.Equal(10, EventBus.Instance.CurrentFrame);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void ReplayBootstrap_SuppressesStateRestored()
    {
        var participant = new FakeParticipant("input", "captured");
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [participant]);
        StateSnapshot snapshot = coordinator.Capture(2, "2.3.0");
        int restored = 0;
        Action<StateRestoredEvent> handler = _ => restored++;
        EventBus.Instance.Subscribe(handler);
        try
        {
            coordinator.Restore(snapshot, SnapshotRestoreMode.ReplayBootstrap);
            Assert.Equal(0, restored);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void StateRestoredSubscriber_CannotPublish()
    {
        var participant = new FakeParticipant("state_machine", "captured");
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [participant]);
        StateSnapshot snapshot = coordinator.Capture(1, "2.3.0");
        bool rejected = false;
        Action<StateRestoredEvent> handler = _ =>
        {
            try { EventBus.Instance.Publish(new ReplayPausedEvent(1)); }
            catch (InvalidOperationException) { rejected = true; }
        };
        EventBus.Instance.Subscribe(handler);
        try
        {
            coordinator.Restore(snapshot, SnapshotRestoreMode.Normal);
            Assert.True(rejected);
        }
        finally { EventBus.Instance.Unsubscribe(handler); }
    }

    [Fact]
    public void CreateRuntime_RequiresCompleteStableParticipantCatalog()
    {
        var incomplete = new[] { new FakeParticipant(SnapshotParticipantCatalog.Input, "value") };
        Assert.Throws<ArgumentException>(() => StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, incomplete));

        IStateSnapshotParticipant[] complete = SnapshotParticipantCatalog.Required
            .Reverse()
            .Select(id => new FakeParticipant(id, id))
            .ToArray();
        var coordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, complete);

        Assert.Equal(SnapshotParticipantCatalog.Required, coordinator.ParticipantOrder);
        Assert.Equal(Enum.GetValues<SnapshotFaultPoint>(), SnapshotFaultCatalog.Locations.Keys.OrderBy(point => point));
    }

    [Theory]
    [InlineData(SnapshotFaultPoint.ReserveEpoch)]
    [InlineData(SnapshotFaultPoint.DecodeComponent)]
    [InlineData(SnapshotFaultPoint.PrepareParticipant)]
    [InlineData(SnapshotFaultPoint.ValidateGraph)]
    public void EveryPrepareFaultPoint_PreservesComponentGraphEpochAndEnvelopeMetadata(SnapshotFaultPoint faultPoint)
    {
        FakeParticipant[] participants = SnapshotParticipantCatalog.Required
            .Select(id => new FakeParticipant(id, $"live-{id}"))
            .ToArray();
        var captureCoordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, participants);
        StateSnapshot snapshot = captureCoordinator.Capture(7, "2.3.0");
        foreach (FakeParticipant participant in participants)
            participant.LiveValue = $"changed-{participant.Discriminator}";
        EventBus.Instance.Publish(new ReplayPausedEvent(7));
        SnapshotAtomicityDiagnostic before = EventBus.Instance.GetSnapshotAtomicityDiagnostic();
        string[] valuesBefore = participants.Select(p => p.LiveValue).ToArray();
        var failingCoordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, participants,
            (point, _) => { if (point == faultPoint) throw new InvalidOperationException($"injected:{point}"); });

        Assert.Throws<SnapshotPrepareException>(() => failingCoordinator.Restore(snapshot, SnapshotRestoreMode.Normal));

        SnapshotAtomicityDiagnostic after = EventBus.Instance.GetSnapshotAtomicityDiagnostic();
        Assert.Equal(valuesBefore, participants.Select(p => p.LiveValue));
        Assert.Equal(before.ActiveEpoch, after.ActiveEpoch);
        Assert.Null(after.ReservedEpoch);
        Assert.Equal(before.Current, after.Current);
        Assert.Equal(before.Next, after.Next);
        Assert.Equal(before.Pending, after.Pending);
        Assert.Equal(before.Quarantined, after.Quarantined);
    }

    [Fact]
    public void RequiredOwnerAdapters_UseVersionedCodecsAndInstallPreparedReferences()
    {
        var live = SnapshotParticipantCatalog.Required.ToDictionary(id => id,
            id => new SnapshotReference<OwnerDto>(new OwnerDto($"old-{id}")));
        IStateSnapshotParticipant[] participants = SnapshotParticipantCatalog.Required.Select(id =>
            new JsonStateSnapshotParticipant<OwnerDto>(id, 1, live[id], (dto, _) => dto)).ToArray();
        var coordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, participants);
        StateSnapshot snapshot = coordinator.Capture(3, "2.3.0");
        foreach (string id in SnapshotParticipantCatalog.Required) live[id].Value = new OwnerDto($"changed-{id}");

        coordinator.Restore(snapshot, SnapshotRestoreMode.ReplayHandoff);

        foreach (string id in SnapshotParticipantCatalog.Required)
            Assert.Equal($"old-{id}", live[id].Value.Value);
    }

    [Fact]
    public void Restore_IncompatibleFramework_RejectsBeforeEpochReservation()
    {
        var participant = new FakeParticipant("input", "live");
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [participant]);
        var snapshot = new StateSnapshot(1, "9.0.0", EventBus.Instance.LifecycleEpoch, 1,
            [new SnapshotComponent("input", 1, "replacement")]);
        ulong epoch = EventBus.Instance.LifecycleEpoch;
        Assert.Throws<SnapshotPrepareException>(() => coordinator.Restore(snapshot, SnapshotRestoreMode.Normal));
        Assert.Equal("live", participant.LiveValue);
        Assert.Equal(epoch, EventBus.Instance.LifecycleEpoch);
    }

    [Fact]
    public void Restore_MaxFrame_RejectsBeforeAnyReferenceSwap()
    {
        var participant = new FakeParticipant("input", "live");
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [participant]);
        var snapshot = new StateSnapshot(1, "2.3.0", EventBus.Instance.LifecycleEpoch, int.MaxValue,
            [new SnapshotComponent("input", 1, "replacement")]);
        Assert.Throws<SnapshotPrepareException>(() => coordinator.Restore(snapshot, SnapshotRestoreMode.Normal));
        Assert.Equal("live", participant.LiveValue);
    }

    [Fact]
    public void Restore_OptionalParticipantMissing_IsSkippedAndRequiredStateRestored()
    {
        // Snapshots recorded before optional participants (combo, training_input)
        // existed, e.g. older replay files, must restore with the optional
        // participants skipped — only the required five are mandatory.
        FakeParticipant[] participants = SnapshotParticipantCatalog.Required
            .Select(id => new FakeParticipant(id, $"live-{id}"))
            .Append(new FakeParticipant(SnapshotParticipantCatalog.Combo, "live-combo"))
            .Append(new FakeParticipant(SnapshotParticipantCatalog.TrainingInput, "live-training"))
            .ToArray();
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, participants);
        var captureCoordinator = new StateSnapshotCoordinator(EventBus.Instance,
            SnapshotParticipantCatalog.Required
                .Select(id => new FakeParticipant(id, $"captured-{id}"))
                .ToArray());
        StateSnapshot snapshot = captureCoordinator.Capture(frame: 3, frameworkVersion: "2.3.0");

        coordinator.Restore(snapshot, SnapshotRestoreMode.Normal);

        foreach (string id in SnapshotParticipantCatalog.Required)
            Assert.Equal($"captured-{id}", participants.First(p => p.Discriminator == id).LiveValue);
        Assert.Equal("live-combo", participants[5].LiveValue);
        Assert.Equal("live-training", participants[6].LiveValue);
    }

    [Fact]
    public void Restore_RequiredParticipantMissing_StillRejects()
    {
        FakeParticipant[] participants = SnapshotParticipantCatalog.Required
            .Select(id => new FakeParticipant(id, $"live-{id}"))
            .Append(new FakeParticipant(SnapshotParticipantCatalog.Combo, "live-combo"))
            .ToArray();
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, participants);
        SnapshotComponent[] components = participants
            .Where(p => p.Discriminator != SnapshotParticipantCatalog.StateMachine)
            .Select(p => p.Capture(0, 1))
            .ToArray();

        var ex = Assert.Throws<SnapshotPrepareException>(() => coordinator.Restore(
            new StateSnapshot(1, "2.3.0", 1, 5, components), SnapshotRestoreMode.Normal));
        Assert.Equal(SnapshotParticipantCatalog.StateMachine, ex.FaultPoint);
    }

    [Fact]
    public void Restore_UntrustedPreparedImplementation_RejectsBeforeAnyReferenceSwap()
    {
        var safe = new FakeParticipant("a", "live-a");
        var unsafeParticipant = new UntrustedParticipant();
        var coordinator = new StateSnapshotCoordinator(EventBus.Instance, [safe, unsafeParticipant]);
        var snapshot = coordinator.Capture(1, "2.3.0");
        safe.LiveValue = "changed-a";
        Assert.Throws<SnapshotPrepareException>(() => coordinator.Restore(snapshot, SnapshotRestoreMode.Normal));
        Assert.Equal("changed-a", safe.LiveValue);
    }

    [Fact]
    public void GameLoopRuntimeCoordinator_RestoresActualOwnerState()
    {
        var move = new MoveDefinition { MoveId = "5LP", Startup = 3, Active = 2, Recovery = 4 };
        var data = new DataStore([move]);
        var stateMachine = new StateMachine(data);
        var inputHistory = new InputHistory(16);
        var chargeTracker = new ChargeTracker(inputHistory);
        var frameData = new FrameDataEngine(data);
        var physics = new PhysicsEngine(data, frameData, stateMachine);
        stateMachine.Initialize(data);
        frameData.Initialize(data);
        physics.Initialize(data);
        try
        {
            stateMachine.InitializePlayer(1);
            stateMachine.InitializePlayer(2);
            stateMachine.PushState(1, CharacterState.Walk);
            inputHistory.RecordInput(1, InputType.Directional, (int)DirectionValue.Back);
            frameData.StartMove(1, "5LP");
            frameData.Update();
            var orchestrator = new ReplayOrchestrator(frameData);
            var coordinator = GameLoop.CreateRuntimeSnapshotCoordinator(
                stateMachine, frameData, physics, inputHistory, chargeTracker, orchestrator);
            orchestrator.AttachSnapshotCoordinator(coordinator);
            StateSnapshot captured = coordinator.Capture(EventBus.Instance.CurrentFrame, "2.3.0");
            int capturedMoveFrame = frameData.GetCurrentFrame(1);

            stateMachine.ReplaceState(1, CharacterState.Crouch);
            inputHistory.RecordInput(1, InputType.Directional, (int)DirectionValue.Forward);
            frameData.Update();

            coordinator.Restore(captured, SnapshotRestoreMode.Normal);

            Assert.Equal(CharacterState.Walk, stateMachine.GetCurrentState(1));
            Assert.Single(inputHistory.GetDirectionalHistory(1));
            Assert.Equal(capturedMoveFrame, frameData.GetCurrentFrame(1));
            Assert.Equal(SnapshotParticipantCatalog.Required, coordinator.ParticipantOrder);
        }
        finally
        {
            physics.Shutdown();
            frameData.Shutdown();
            chargeTracker.Shutdown();
            inputHistory.Shutdown();
            stateMachine.Shutdown();
        }
    }

    private sealed record OwnerDto(string Value);

    private sealed class UntrustedParticipant : IStateSnapshotParticipant
    {
        public string Discriminator => "z";
        public int CodecVersion => 1;
        public SnapshotComponent Capture(int frame, ulong epoch) => new(Discriminator, 1, "value");
        public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context) =>
            new UntrustedPrepared();
        private sealed class UntrustedPrepared : IPreparedSnapshotComponent { public string Discriminator => "z"; }
    }

    private sealed class FakeParticipant : IStateSnapshotParticipant
    {
        private readonly SnapshotReference<string> _slot;
        public FakeParticipant(string discriminator, string value)
        {
            Discriminator = discriminator;
            _slot = new SnapshotReference<string>(value);
        }

        public string Discriminator { get; }
        public int CodecVersion => 1;
        public string LiveValue { get => _slot.Value; set => _slot.Value = value; }
        public bool FailPrepare { get; init; }
        public SnapshotComponent Capture(int frame, ulong epoch) => new(Discriminator, CodecVersion, LiveValue);
        public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
        {
            if (FailPrepare) throw new SnapshotPrepareException(Discriminator, "injected");
            return PreparedSnapshotComponent.Create(Discriminator, _slot, component.Payload);
        }
    }
}
