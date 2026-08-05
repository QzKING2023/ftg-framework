#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Input;

namespace FTG_Framework.Core;

/// <summary>
/// Cross-component validation for training snapshots. Runs at the coordinator's
/// ValidateGraph seam against the candidate components only — no live mutation.
/// Every failure is reported as a SnapshotPrepareException at the
/// "cross_component_graph" fault point so the whole candidate is rejected.
/// </summary>
internal static class TrainingSnapshotGraphValidator
{
    private const string FaultPoint = "cross_component_graph";
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = false };

    internal static Action<IReadOnlyDictionary<string, SnapshotComponent>, SnapshotPrepareContext> Create(
        IDataStore? dataStore) =>
        (byId, context) => Validate(byId, context, dataStore);

    internal static void Validate(IReadOnlyDictionary<string, SnapshotComponent> byId,
        SnapshotPrepareContext context, IDataStore? dataStore)
    {
        ArgumentNullException.ThrowIfNull(byId);
        ValidateFrameConsistency(byId, context);
        ValidateStateMachine(byId, context);
        ValidatePhysics(byId, context);
        ValidateFrameDataMoves(byId, dataStore);
        ValidateCombo(byId, context, dataStore);
        ValidateTrainingInput(byId, context);
    }

    private static void ValidateFrameConsistency(
        IReadOnlyDictionary<string, SnapshotComponent> byId, SnapshotPrepareContext context)
    {
        FrameDataRuntimeSnapshot frameData = Decode<FrameDataRuntimeSnapshot>(byId, SnapshotParticipantCatalog.FrameData);
        if (frameData.State.Frame != context.Frame)
            throw Fail($"frame_data completed frame {frameData.State.Frame} does not match snapshot frame {context.Frame}.");
    }

    private static void ValidateStateMachine(
        IReadOnlyDictionary<string, SnapshotComponent> byId, SnapshotPrepareContext context)
    {
        StateMachineRuntimeSnapshot stateMachine = Decode<StateMachineRuntimeSnapshot>(
            byId, SnapshotParticipantCatalog.StateMachine);
        ValidatePlayerKeys(stateMachine.Stacks.Keys, "state_machine stacks", allowEmpty: false);
        ValidatePlayerKeys(stateMachine.GenerationHighWater.Keys, "state_machine generation high-water", allowEmpty: true);
        foreach (var (playerId, stack) in stateMachine.Stacks)
        {
            if (stack.Count == 0)
                throw Fail($"P{playerId} state stack is empty.");
            foreach (CharacterState state in stack)
            {
                if (!Enum.IsDefined(state))
                    throw Fail($"P{playerId} state stack contains unknown state {state}.");
            }
        }
        if (stateMachine.KnockbackTuples is not null)
        {
            ValidatePlayerKeys(stateMachine.KnockbackTuples.Keys, "state_machine knockback tuples", allowEmpty: true);
            foreach (var (playerId, tuple) in stateMachine.KnockbackTuples)
            {
                if (tuple.Generation == 0)
                    throw Fail($"P{playerId} knockback tuple has generation 0.");
                if (!tuple.Terminal &&
                    (stateMachine.HitstunOccupancy is null || !stateMachine.HitstunOccupancy.ContainsKey(playerId)))
                    throw Fail($"P{playerId} non-terminal knockback tuple has no hitstun occupancy.");
            }
        }
        if (stateMachine.HitstunOccupancy is not null)
        {
            ValidatePlayerKeys(stateMachine.HitstunOccupancy.Keys, "state_machine hitstun occupancy", allowEmpty: true);
            foreach (var (playerId, owner) in stateMachine.HitstunOccupancy)
            {
                if (stateMachine.KnockbackTuples is null || !stateMachine.KnockbackTuples.ContainsKey(playerId))
                    throw Fail($"P{playerId} hitstun occupancy has no tracked knockback tuple.");
                if (stateMachine.KnockbackTuples.TryGetValue(playerId, out var tuple) &&
                    owner.Generation > tuple.Generation)
                    throw Fail(
                        $"P{playerId} hitstun occupancy generation {owner.Generation} exceeds its tracked tuple generation {tuple.Generation}.");
            }
        }
    }

    private static void ValidatePhysics(
        IReadOnlyDictionary<string, SnapshotComponent> byId, SnapshotPrepareContext context)
    {
        PhysicsRuntimeSnapshot physics = Decode<PhysicsRuntimeSnapshot>(byId, SnapshotParticipantCatalog.PhysicsMotion);
        ValidatePlayerKeys(physics.Participants.Keys, "physics participants", allowEmpty: true);
        ValidatePlayerKeys(physics.Motions.Keys, "physics motions", allowEmpty: true);
        ValidatePlayerKeys(physics.GenerationHighWater.Keys, "physics generation high-water", allowEmpty: true);
        foreach (var (playerId, trajectory) in physics.Trajectories)
        {
            if (playerId is not (1 or 2))
                throw Fail($"Trajectory player '{playerId}' is invalid; only players 1 and 2 exist.");
            if (trajectory.GenerationId == 0)
                throw Fail($"P{playerId} trajectory has generation 0, which is never assigned.");
        }
        foreach (var (playerId, trajectory) in physics.Trajectories.Where(item => !item.Value.Completed))
        {
            // The high-water is the last assigned generation; ReserveGeneration
            // launches next at high-water + 1, which keeps every new launch above
            // every restored in-flight generation (AC-13). A generation above the
            // high-water would corrupt that guarantee.
            ulong highWater = physics.GenerationHighWater.TryGetValue(playerId, out ulong value) ? value : 0;
            if (trajectory.GenerationId > highWater)
                throw Fail(
                    $"P{playerId} in-flight trajectory generation {trajectory.GenerationId} exceeds the prepared generation high-water {highWater}.");
        }
    }

    private static void ValidateFrameDataMoves(
        IReadOnlyDictionary<string, SnapshotComponent> byId, IDataStore? dataStore)
    {
        if (dataStore is null) return;
        FrameDataRuntimeSnapshot frameData = Decode<FrameDataRuntimeSnapshot>(byId, SnapshotParticipantCatalog.FrameData);
        ValidateMoveInstance(frameData.State.P1MoveId, frameData.State.P1Phase, 1, dataStore);
        ValidateMoveInstance(frameData.State.P2MoveId, frameData.State.P2Phase, 2, dataStore);

        static void ValidateMoveInstance(string? moveId, MovePhase phase, int player, IDataStore store)
        {
            if (phase == MovePhase.Idle)
            {
                if (!string.IsNullOrWhiteSpace(moveId))
                    throw Fail($"P{player} reports idle phase with move identity '{moveId}'.");
                return;
            }
            if (string.IsNullOrWhiteSpace(moveId))
                throw Fail($"P{player} reports phase {phase} without a move identity.");
            if (store.GetMove(moveId) is null)
                throw Fail($"P{player} move '{moveId}' has no registered definition (dangling reference).");
        }
    }

    private static void ValidateCombo(IReadOnlyDictionary<string, SnapshotComponent> byId,
        SnapshotPrepareContext context, IDataStore? dataStore)
    {
        if (!byId.TryGetValue(SnapshotParticipantCatalog.Combo, out SnapshotComponent? component)) return;
        ComboRuntimeSnapshot combo = DecodePayload<ComboRuntimeSnapshot>(component, SnapshotParticipantCatalog.Combo);
        ValidatePlayerKeys(combo.Tracks.Keys, "combo tracks", allowEmpty: true);
        foreach (var (playerId, track) in combo.Tracks)
        {
            if (track.Active)
            {
                // The dispatch counter advances before subscribers observe it, so a
                // track that started in the final dispatched frame records frame + 1.
                // Replay bootstrap/handoff captures run in the live frame domain
                // while the snapshot frame is the replay domain; tolerate the
                // live-domain start there (the participant rebases it).
                int upperBound = context.Mode == SnapshotRestoreMode.Normal ? context.Frame + 1 : int.MaxValue;
                if (track.StartFrame < 0 || track.StartFrame > upperBound)
                    throw Fail($"P{playerId} active combo start frame {track.StartFrame} is inconsistent with snapshot frame {context.Frame}.");
                if (dataStore is not null && !string.IsNullOrWhiteSpace(track.CurrentMoveId) &&
                    dataStore.GetMove(track.CurrentMoveId) is null)
                    throw Fail($"P{playerId} combo move '{track.CurrentMoveId}' has no registered definition (dangling reference).");
            }
        }
    }

    private static void ValidateTrainingInput(IReadOnlyDictionary<string, SnapshotComponent> byId,
        SnapshotPrepareContext context)
    {
        if (!byId.TryGetValue(SnapshotParticipantCatalog.TrainingInput, out SnapshotComponent? component)) return;
        TrainingInputRuntimeSnapshot training = DecodePayload<TrainingInputRuntimeSnapshot>(
            component, SnapshotParticipantCatalog.TrainingInput);
        if (training.Session is { } session && session.Epoch != context.SourceEpoch)
            throw Fail(
                $"Training playback session epoch {session.Epoch} does not match snapshot source epoch {context.SourceEpoch}.");
    }

    private static TState Decode<TState>(IReadOnlyDictionary<string, SnapshotComponent> byId, string discriminator)
        where TState : class
    {
        if (!byId.TryGetValue(discriminator, out SnapshotComponent? component))
            throw Fail($"Required component '{discriminator}' is missing.");
        return DecodePayload<TState>(component, discriminator);
    }

    private static TState DecodePayload<TState>(SnapshotComponent component, string discriminator)
        where TState : class
    {
        try
        {
            return JsonSerializer.Deserialize<TState>(component.Payload, Options)
                ?? throw new SnapshotPrepareException(FaultPoint,
                    $"Component '{discriminator}' decoded to null.");
        }
        catch (JsonException ex)
        {
            throw new SnapshotPrepareException(FaultPoint,
                $"Component '{discriminator}' is not a valid value snapshot.", ex);
        }
    }

    private static void ValidatePlayerKeys(IEnumerable<int> keys, string subject, bool allowEmpty)
    {
        var materialized = keys.ToArray();
        if (!allowEmpty && materialized.Length == 0)
            throw Fail($"{subject} declares no players.");
        foreach (int playerId in materialized)
        {
            if (playerId is not (1 or 2))
                throw Fail($"{subject} contains invalid player '{playerId}'; only players 1 and 2 exist.");
        }
    }

    private static SnapshotPrepareException Fail(string message) =>
        new(FaultPoint, message);
}
