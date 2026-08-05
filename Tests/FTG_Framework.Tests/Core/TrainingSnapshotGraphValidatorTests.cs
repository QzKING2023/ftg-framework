#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Data;
using FTG_Framework.Engine.StateMachine;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class TrainingSnapshotGraphValidatorTests
{
    private static SnapshotPrepareContext Context(int frame = 10, ulong source = 1, ulong reserved = 2) =>
        new(source, reserved, frame, SnapshotRestoreMode.Normal);

    private static SnapshotComponent Component<TState>(string discriminator, TState value) =>
        new(discriminator, 1, System.Text.Json.JsonSerializer.Serialize(value));

    private static Dictionary<string, SnapshotComponent> HappyGraph(int frame = 10)
    {
        var byId = new Dictionary<string, SnapshotComponent>(StringComparer.Ordinal)
        {
            [SnapshotParticipantCatalog.StateMachine] = Component(
                SnapshotParticipantCatalog.StateMachine,
                new StateMachineRuntimeSnapshot(
                    new Dictionary<int, List<CharacterState>>
                    {
                        [1] = new() { CharacterState.Idle },
                        [2] = new() { CharacterState.Idle }
                    },
                    new Dictionary<int, Data.PhysicsResponseProfile>(),
                    new Dictionary<int, GenerationEpochSnapshot>(),
                    new Dictionary<int, ReactionRecoverySnapshot>())),
            [SnapshotParticipantCatalog.FrameData] = Component(
                SnapshotParticipantCatalog.FrameData,
                new FrameDataRuntimeSnapshot(new FrameStateSnapshot(frame, null, 0, MovePhase.Idle, null, 0, MovePhase.Idle))),
            [SnapshotParticipantCatalog.PhysicsMotion] = Component(
                SnapshotParticipantCatalog.PhysicsMotion,
                new PhysicsRuntimeSnapshot(
                    new Dictionary<int, ulong>(),
                    new Dictionary<int, PhysicsParticipantSnapshot>(),
                    new Dictionary<int, PhysicsMotionSnapshot>(),
                    new HashSet<PhysicsConsumedContact>(),
                    new Dictionary<int, PhysicsTrajectorySnapshot>())),
            [SnapshotParticipantCatalog.Input] = Component(
                SnapshotParticipantCatalog.Input,
                new InputRuntimeSnapshot(
                    Array.Empty<InputEntry>(), Array.Empty<InputEntry>(),
                    Array.Empty<InputEntry>(), Array.Empty<InputEntry>(),
                    Array.Empty<int>(), Array.Empty<int>(), Array.Empty<bool>(), frame))
        };
        return byId;
    }

    [Fact]
    public void Validate_HappyGraph_Passes()
    {
        TrainingSnapshotGraphValidator.Validate(HappyGraph(), Context(), dataStore: null);
    }

    [Fact]
    public void Validate_FrameDataFrameMismatch_Rejects()
    {
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.FrameData] = Component(
            SnapshotParticipantCatalog.FrameData,
            new FrameDataRuntimeSnapshot(new FrameStateSnapshot(5, null, 0, MovePhase.Idle, null, 0, MovePhase.Idle)));

        var ex = Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(10), dataStore: null));
        Assert.Equal("cross_component_graph", ex.FaultPoint);
        Assert.Contains("frame", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MissingRequiredComponent_Rejects()
    {
        var byId = HappyGraph();
        byId.Remove(SnapshotParticipantCatalog.PhysicsMotion);
        Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null));
    }

    [Fact]
    public void Validate_EmptyStateStack_Rejects()
    {
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.StateMachine] = Component(
            SnapshotParticipantCatalog.StateMachine,
            new StateMachineRuntimeSnapshot(
                new Dictionary<int, List<CharacterState>> { [1] = new() },
                new Dictionary<int, Data.PhysicsResponseProfile>(),
                new Dictionary<int, GenerationEpochSnapshot>(),
                new Dictionary<int, ReactionRecoverySnapshot>()));
        Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null));
    }

    [Fact]
    public void Validate_InFlightTrajectoryAboveHighWater_Rejects()
    {
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.PhysicsMotion] = Component(
            SnapshotParticipantCatalog.PhysicsMotion,
            new PhysicsRuntimeSnapshot(
                new Dictionary<int, ulong> { [1] = 3 },
                new Dictionary<int, PhysicsParticipantSnapshot>(),
                new Dictionary<int, PhysicsMotionSnapshot>(),
                new HashSet<PhysicsConsumedContact>(),
                new Dictionary<int, PhysicsTrajectorySnapshot>
                {
                    [1] = new PhysicsTrajectorySnapshot(5, 0, 0, 0, 0, 0, false, default!, 0, false)
                }));
        var ex = Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null));
        Assert.Contains("high-water", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_InFlightTrajectoryAtHighWater_Passes()
    {
        // The counter is the last assigned generation; a first knockback is
        // in-flight at generation == high-water and the next launch is +1.
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.PhysicsMotion] = Component(
            SnapshotParticipantCatalog.PhysicsMotion,
            new PhysicsRuntimeSnapshot(
                new Dictionary<int, ulong> { [1] = 5 },
                new Dictionary<int, PhysicsParticipantSnapshot>(),
                new Dictionary<int, PhysicsMotionSnapshot>(),
                new HashSet<PhysicsConsumedContact>(),
                new Dictionary<int, PhysicsTrajectorySnapshot>
                {
                    [1] = new PhysicsTrajectorySnapshot(5, 0, 0, 0, 0, 0, false, default!, 0, false)
                }));
        TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null);
    }

    [Fact]
    public void Validate_InFlightTrajectoryBelowHighWater_Passes()
    {
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.PhysicsMotion] = Component(
            SnapshotParticipantCatalog.PhysicsMotion,
            new PhysicsRuntimeSnapshot(
                new Dictionary<int, ulong> { [1] = 6 },
                new Dictionary<int, PhysicsParticipantSnapshot>(),
                new Dictionary<int, PhysicsMotionSnapshot>(),
                new HashSet<PhysicsConsumedContact>(),
                new Dictionary<int, PhysicsTrajectorySnapshot>
                {
                    [1] = new PhysicsTrajectorySnapshot(5, 0, 0, 0, 0, 0, false, default!, 0, false)
                }));
        TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null);
    }

    [Fact]
    public void Validate_DanglingMoveReference_Rejects()
    {
        var store = new DataStore([]);
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.FrameData] = Component(
            SnapshotParticipantCatalog.FrameData,
            new FrameDataRuntimeSnapshot(new FrameStateSnapshot(10, "5LP", 2, MovePhase.Active, null, 0, MovePhase.Idle)));
        var ex = Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: store));
        Assert.Contains("5LP", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_KnownMoveReference_Passes()
    {
        var store = new DataStore([new MoveDefinition { MoveId = "5LP", Startup = 3, Active = 2, Recovery = 4 }]);
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.FrameData] = Component(
            SnapshotParticipantCatalog.FrameData,
            new FrameDataRuntimeSnapshot(new FrameStateSnapshot(10, "5LP", 2, MovePhase.Active, null, 0, MovePhase.Idle)));
        TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: store);
    }

    [Fact]
    public void Validate_ComboMoveDanglingReference_Rejects()
    {
        var store = new DataStore([]);
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.Combo] = Component(
            SnapshotParticipantCatalog.Combo,
            new ComboRuntimeSnapshot(new Dictionary<int, ComboTrackRuntimeSnapshot>
            {
                [1] = new ComboTrackRuntimeSnapshot(true, 1, "ghost-move", 1, 1, false, "ghost-move")
            }));
        Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: store));
    }

    [Fact]
    public void Validate_SessionEpochMismatch_Rejects()
    {
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.TrainingInput] = Component(
            SnapshotParticipantCatalog.TrainingInput,
            new TrainingInputRuntimeSnapshot(
                new[] { new TrainingInputRecordingRuntimeSnapshot("r", "cA==") },
                null, null, null,
                new TrainingInputPlaybackSessionRuntimeSnapshot("r", 2, 0, 0, false, false, false, 9)));
        var ex = Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(source: 1), dataStore: null));
        Assert.Contains("epoch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NormalMode_ComboStartFrameBeyondFrameBound_Rejects()
    {
        var byId = HappyGraph(frame: 10);
        byId[SnapshotParticipantCatalog.Combo] = Component(
            SnapshotParticipantCatalog.Combo,
            new ComboRuntimeSnapshot(new Dictionary<int, ComboTrackRuntimeSnapshot>
            {
                [1] = new ComboTrackRuntimeSnapshot(true, 1, "jab", 100, 1, false, "jab")
            }));
        Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null));
    }

    [Fact]
    public void Validate_ReplayBootstrapMode_ComboStartFrameBeyondNormalBound_Passes()
    {
        var byId = HappyGraph(frame: 10);
        byId[SnapshotParticipantCatalog.Combo] = Component(
            SnapshotParticipantCatalog.Combo,
            new ComboRuntimeSnapshot(new Dictionary<int, ComboTrackRuntimeSnapshot>
            {
                [1] = new ComboTrackRuntimeSnapshot(true, 1, "jab", 100, 1, false, "jab")
            }));
        TrainingSnapshotGraphValidator.Validate(
            byId, Context() with { Mode = SnapshotRestoreMode.ReplayBootstrap }, dataStore: null);
    }

    [Fact]
    public void Validate_ReplayBootstrapMode_NegativeComboStartFrame_Rejects()
    {
        var byId = HappyGraph(frame: 10);
        byId[SnapshotParticipantCatalog.Combo] = Component(
            SnapshotParticipantCatalog.Combo,
            new ComboRuntimeSnapshot(new Dictionary<int, ComboTrackRuntimeSnapshot>
            {
                [1] = new ComboTrackRuntimeSnapshot(true, 1, "jab", -1, 1, false, "jab")
            }));
        Assert.Throws<SnapshotPrepareException>(() => TrainingSnapshotGraphValidator.Validate(
            byId, Context() with { Mode = SnapshotRestoreMode.ReplayBootstrap }, dataStore: null));
    }

    [Fact]
    public void Validate_UnknownPlayerTrajectory_Rejects()
    {
        var byId = HappyGraph();
        byId[SnapshotParticipantCatalog.PhysicsMotion] = Component(
            SnapshotParticipantCatalog.PhysicsMotion,
            new PhysicsRuntimeSnapshot(
                new Dictionary<int, ulong>(),
                new Dictionary<int, PhysicsParticipantSnapshot>(),
                new Dictionary<int, PhysicsMotionSnapshot>(),
                new HashSet<PhysicsConsumedContact>(),
                new Dictionary<int, PhysicsTrajectorySnapshot>
                {
                    [4] = new PhysicsTrajectorySnapshot(1, 0, 0, 0, 0, 0, false, default!, 0, true)
                }));
        Assert.Throws<SnapshotPrepareException>(() =>
            TrainingSnapshotGraphValidator.Validate(byId, Context(), dataStore: null));
    }
}
