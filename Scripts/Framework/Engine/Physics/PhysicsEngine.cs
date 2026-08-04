#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Input;

namespace FTG_Framework.Engine.Physics;

internal sealed class PhysicsEngine : IPhysicsEngine
{
    private readonly IDataStore _dataStore;
    private readonly IFrameDataEngine _frameData;
    private readonly IStateMachine? _stateMachine;
    private readonly SortedDictionary<int, IPhysicsParticipant> _participants = new();
    private readonly HashSet<HitKey> _previousActive = new();
    private readonly HashSet<HitKey> _currentActive = new();
    private HashSet<PhysicsConsumedContact> _consumedContacts = new();
    private readonly PhysicsConsumedContact[] _consumedThisUpdate = new PhysicsConsumedContact[2];
    private int _consumedThisUpdateCount;
    private readonly Dictionary<ContextKey, HitContext> _contexts = new();
    private readonly Dictionary<int, TrajectoryState> _trajectories = new();
    private readonly Dictionary<int, LaunchCandidate> _launchCandidates = new();
    private readonly Dictionary<int, LocomotionCommand> _locomotionCommands = new();
    private Dictionary<int, ulong> _generationCounters = new();
    private readonly List<object> _pendingCollisionEvents = new();
    private readonly List<PhysicsConsumedContact> _contactsToPrune = new(2);
    private bool _initialized;

    private readonly record struct HitKey(
        long MoveInstanceId, int AttackerId, int DefenderId, string HitboxId);
    private readonly record struct ContextKey(
        int AttackerId, int DefenderId, string HitboxId, int ContactFrame);
    private readonly record struct LaunchCandidate(
        TrajectoryState Trajectory, float EffectiveFriction, float GravityScale);

    internal PhysicsEngine(
        IDataStore dataStore, IFrameDataEngine frameData, IStateMachine? stateMachine = null)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(frameData);
        _dataStore = dataStore;
        _frameData = frameData;
        _stateMachine = stateMachine;
    }

    public void Initialize(IDataStore dataStore) => _initialized = true;

    public void Shutdown()
    {
        _initialized = false;
        _participants.Clear();
        _previousActive.Clear();
        _currentActive.Clear();
        _consumedContacts.Clear();
        _contexts.Clear();
        _trajectories.Clear();
        _launchCandidates.Clear();
        _generationCounters.Clear();
        _pendingCollisionEvents.Clear();
        _contactsToPrune.Clear();
        _consumedThisUpdateCount = 0;
        _locomotionCommands.Clear();
    }

    public void Register(IPhysicsParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (participant.PlayerId < 1 || participant.PlayerId > 2)
            throw new ArgumentOutOfRangeException(nameof(participant), "[Physics] PlayerId must be 1 or 2.");
        ValidateSnapshotIdentity(participant, participant.CapturePhysicsSnapshot());
        _trajectories.Remove(participant.PlayerId);
        _participants[participant.PlayerId] = participant;
    }

    public void Unregister(IPhysicsParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        int playerId = participant.PlayerId;
        if (!_participants.TryGetValue(playerId, out var registered) ||
            !ReferenceEquals(registered, participant))
            return;
        _participants.Remove(playerId);
        _trajectories.Remove(playerId);
        _launchCandidates.Remove(playerId);
        _previousActive.RemoveWhere(k => k.AttackerId == playerId || k.DefenderId == playerId);
        _consumedContacts.RemoveWhere(k => k.AttackerId == playerId || k.DefenderId == playerId);
        _locomotionCommands.Remove(playerId);
    }

    public void SetLocomotionCommand(LocomotionCommand command)
    {
        if (command.PlayerId is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(command), "[Physics] PlayerId must be 1 or 2.");
        if (command.WorldAxis is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(command), "[Physics] WorldAxis must be -1, 0, or 1.");
        _locomotionCommands[command.PlayerId] = command;
    }

    public PhysicsFacingSnapshot CaptureFacingSnapshot()
    {
        if (!_participants.TryGetValue(1, out var p1) || !_participants.TryGetValue(2, out var p2))
            return new PhysicsFacingSnapshot(true, false, false);
        var first = p1.CapturePhysicsSnapshot();
        var second = p2.CapturePhysicsSnapshot();
        var facing = FacingResolver.Resolve(
            first.WorldX, second.WorldX,
            first.FacingRight ? AuthoritativeFacing.Right : AuthoritativeFacing.Left,
            second.FacingRight ? AuthoritativeFacing.Right : AuthoritativeFacing.Left);
        return new PhysicsFacingSnapshot(
            facing.P1 == AuthoritativeFacing.Right,
            facing.P2 == AuthoritativeFacing.Right,
            facing.BlockEligible);
    }

    public bool TryGetHitContext(
        int attackerId, int defenderId, string hitboxId, int contactFrame,
        out HitContext context) =>
        _contexts.TryGetValue(new ContextKey(attackerId, defenderId, hitboxId, contactFrame), out context);

    public void Update()
    {
        if (!_initialized)
            return;
        _consumedThisUpdateCount = 0;
        _contexts.Clear();
        _currentActive.Clear();
        _launchCandidates.Clear();
        _pendingCollisionEvents.Clear();
        PruneConsumedContacts();
        bool hadP1Generation = _generationCounters.TryGetValue(1, out ulong p1Generation);
        bool hadP2Generation = _generationCounters.TryGetValue(2, out ulong p2Generation);
        if (_participants.Count == 2)
        {
            try
            {
                var firstParticipant = _participants[1];
                var secondParticipant = _participants[2];
                var first = firstParticipant.CapturePhysicsSnapshot();
                var second = secondParticipant.CapturePhysicsSnapshot();
                ValidateSnapshotIdentity(firstParticipant, first);
                ValidateSnapshotIdentity(secondParticipant, second);
                var facing = FacingResolver.Resolve(
                    first.WorldX, second.WorldX,
                    first.FacingRight ? AuthoritativeFacing.Right : AuthoritativeFacing.Left,
                    second.FacingRight ? AuthoritativeFacing.Right : AuthoritativeFacing.Left);
                first = AdvanceLocomotion(firstParticipant, first, facing.P1);
                second = AdvanceLocomotion(secondParticipant, second, facing.P2);
                Detect(first, second);
                Detect(second, first);
            }
            catch
            {
                _generationCounters.Clear();
                if (hadP1Generation) _generationCounters[1] = p1Generation;
                if (hadP2Generation) _generationCounters[2] = p2Generation;
                _contexts.Clear();
                _currentActive.Clear();
                _launchCandidates.Clear();
                _pendingCollisionEvents.Clear();
                for (int i = 0; i < _consumedThisUpdateCount; i++)
                    _consumedContacts.Remove(_consumedThisUpdate[i]);
                _consumedThisUpdateCount = 0;
                throw;
            }
        }
        foreach (var pending in _pendingCollisionEvents)
        {
            if (pending is HitConnectedEvent hit) EventBus.Instance.Publish(hit);
            else if (pending is MoveBlockedEvent blocked) EventBus.Instance.Publish(blocked);
        }
        AdvanceTrajectories();
        foreach (var candidate in _launchCandidates)
        {
            _trajectories[candidate.Key] = candidate.Value.Trajectory;
            PublishTrajectory(candidate.Key, candidate.Value.Trajectory, KnockbackPhase.Started,
                candidate.Value.EffectiveFriction, candidate.Value.GravityScale);
        }
        _previousActive.Clear();
        foreach (var key in _currentActive)
            _previousActive.Add(key);
    }

    private PhysicsParticipantSnapshot AdvanceLocomotion(
        IPhysicsParticipant participant, PhysicsParticipantSnapshot snapshot, AuthoritativeFacing facing)
    {
        var motion = participant.CaptureMotionSnapshot();
        snapshot = snapshot with { FacingRight = facing == AuthoritativeFacing.Right };
        _locomotionCommands.TryGetValue(snapshot.PlayerId, out var command);
        CharacterState state = _stateMachine?.GetCurrentState(snapshot.PlayerId) ?? CharacterState.Idle;
        bool actionable = state is CharacterState.Idle or CharacterState.Walk or CharacterState.Crouch or
            CharacterState.JumpStartup or CharacterState.JumpActive or CharacterState.JumpRecovery;
        bool trajectoryOwnsMovement = _trajectories.ContainsKey(snapshot.PlayerId);

        if (!trajectoryOwnsMovement && motion.Airborne)
        {
            float y = snapshot.WorldY + motion.VelocityY;
            float velocityY = motion.VelocityY + 0.5f;
            if (velocityY >= 0 && y >= motion.GroundY)
            {
                y = motion.GroundY;
                motion = new PhysicsMotionSnapshot(0, 0, false, motion.GroundY);
                if (_stateMachine is not null)
                    _stateMachine.ReplaceState(snapshot.PlayerId, CharacterState.JumpRecovery);
            }
            else
            {
                motion = motion with { VelocityY = velocityY };
                if (_stateMachine is not null && state != CharacterState.JumpActive)
                    _stateMachine.ReplaceState(snapshot.PlayerId, CharacterState.JumpActive);
            }
            snapshot = snapshot with { WorldY = y };
        }
        else if (!trajectoryOwnsMovement && actionable && command.JumpPressed && !motion.Airborne)
        {
            motion = new PhysicsMotionSnapshot(0, -8f, true, snapshot.WorldY);
            snapshot = snapshot with { WorldY = snapshot.WorldY - 8f };
            motion = motion with { VelocityY = -7.5f };
            _stateMachine?.PushState(snapshot.PlayerId, CharacterState.JumpStartup);
            _stateMachine?.ReplaceState(snapshot.PlayerId, CharacterState.JumpActive);
        }
        else if (!trajectoryOwnsMovement && actionable && !motion.Airborne)
        {
            if (command.CrouchHeld)
            {
                _stateMachine?.ReplaceState(snapshot.PlayerId, CharacterState.Crouch);
            }
            else if (command.WorldAxis != 0)
            {
                snapshot = snapshot with { WorldX = snapshot.WorldX + command.WorldAxis * 3f };
                _stateMachine?.ReplaceState(snapshot.PlayerId, CharacterState.Walk);
            }
            else if (state is CharacterState.Walk or CharacterState.Crouch or CharacterState.JumpRecovery)
            {
                _stateMachine?.ReplaceState(snapshot.PlayerId, CharacterState.Idle);
            }
        }

        participant.ApplyPhysicsState(snapshot, motion);
        return snapshot;
    }

    private void Detect(in PhysicsParticipantSnapshot attacker, in PhysicsParticipantSnapshot defender)
    {
        var evaluated = _frameData.GetLastEvaluatedFrame(attacker.PlayerId);
        if (evaluated.Phase != MovePhase.Active || evaluated.MoveId is null)
            return;
        var move = _dataStore.GetMove(evaluated.MoveId);
        if (move is null)
            return;
        var collisionFrame = FindFrame(move.CollisionFrames, evaluated.AuthoredFrame);
        if (collisionFrame is null || collisionFrame.Hitboxes.Count == 0)
            return;

        var defenderFrame = _frameData.GetLastEvaluatedFrame(defender.PlayerId);
        IReadOnlyList<CollisionBoxDefinition> hurtboxes = defender.NeutralHurtboxes;
        if (defenderFrame.MoveId is not null)
        {
            var defenderMove = _dataStore.GetMove(defenderFrame.MoveId);
            var frame = defenderMove is null ? null : FindFrame(defenderMove.CollisionFrames, defenderFrame.AuthoredFrame);
            hurtboxes = frame?.Hurtboxes ?? Array.Empty<CollisionBoxDefinition>();
        }

        int contactFrame = EventBus.Instance.CurrentFrame;
        for (int hitIndex = 0; hitIndex < collisionFrame.Hitboxes.Count; hitIndex++)
        {
            var hitbox = collisionFrame.Hitboxes[hitIndex];
            bool overlaps = false;
            var worldHitbox = CollisionDetector.ToWorld(
                hitbox, attacker.WorldX, attacker.WorldY, attacker.FacingRight);
            for (int hurtIndex = 0; hurtIndex < hurtboxes.Count; hurtIndex++)
            {
                var hurtbox = hurtboxes[hurtIndex];
                var worldHurtbox = CollisionDetector.ToWorld(
                    hurtbox, defender.WorldX, defender.WorldY, defender.FacingRight);
                if (CollisionDetector.Overlaps(worldHitbox, worldHurtbox))
                {
                    overlaps = true;
                    break;
                }
            }
            if (!overlaps)
                continue;

            var consumed = new PhysicsConsumedContact(
                evaluated.MoveInstanceId, attacker.PlayerId, defender.PlayerId);
            if (_consumedContacts.Contains(consumed))
                continue;

            var hitKey = new HitKey(
                evaluated.MoveInstanceId, attacker.PlayerId, defender.PlayerId, hitbox.BoxId);
            _currentActive.Add(hitKey);
            if (_previousActive.Contains(hitKey))
                continue;

            CharacterState defenderState = _stateMachine?.GetCurrentState(defender.PlayerId) ?? CharacterState.Idle;
            if (IsBlocking(defender.Direction, attacker.WorldX, defender.WorldX, defenderState,
                    _participants[defender.PlayerId].CaptureMotionSnapshot().Airborne))
            {
                ConsumeContact(consumed);
                _pendingCollisionEvents.Add(new MoveBlockedEvent(
                    attacker.PlayerId, defender.PlayerId, move.MoveId,
                    move.BlockAdvantage, move.Damage, contactFrame));
                continue;
            }

            var hitEvent = new HitConnectedEvent(
                attacker.PlayerId, defender.PlayerId, move.MoveId,
                move.HitAdvantage, move.Damage, contactFrame, hitbox.BoxId);
            KnockbackProfile? profile = move.KnockbackProfileId is null
                ? null
                : _dataStore.GetKnockbackProfile(move.KnockbackProfileId);
            if (profile is not null)
                profile = Snapshot.Of(profile);
            LaunchCandidate? launchCandidate = null;
            if (profile is not null && _stateMachine is not null &&
                _participants.TryGetValue(defender.PlayerId, out var participant))
            {
                var response = _stateMachine.GetEffectivePhysicsProfile(defender.PlayerId);
                var motion = participant.CaptureMotionSnapshot();
                ulong generation = ReserveGeneration(defender.PlayerId);
                var trajectory = ForceCalculator.Launch(
                    generation,
                    defender.WorldX,
                    defender.WorldY,
                    attacker.WorldX,
                    attacker.FacingRight,
                    motion,
                    profile,
                    response,
                    contactFrame);
                float effectiveFriction = profile.Friction *
                    (trajectory.Airborne ? response.AirFriction : response.Friction);
                launchCandidate = new LaunchCandidate(trajectory, effectiveFriction, response.GravityScale);
            }
            ConsumeContact(consumed);
            _pendingCollisionEvents.Add(hitEvent);
            var context = new HitContext(hitEvent, hitbox.BoxId, profile);
            _contexts[new ContextKey(
                attacker.PlayerId, defender.PlayerId, hitbox.BoxId, contactFrame)] = context;
            if (launchCandidate.HasValue)
                _launchCandidates[defender.PlayerId] = launchCandidate.Value;
        }
    }

    private void ConsumeContact(PhysicsConsumedContact contact)
    {
        if (!_consumedContacts.Add(contact)) return;
        if (_consumedThisUpdateCount >= _consumedThisUpdate.Length)
            throw new InvalidOperationException("[Physics] More contact outcomes were consumed than the two-player update permits.");
        _consumedThisUpdate[_consumedThisUpdateCount++] = contact;
    }

    private void PruneConsumedContacts()
    {
        _contactsToPrune.Clear();
        foreach (var contact in _consumedContacts)
        {
            var frame = _frameData.GetLastEvaluatedFrame(contact.AttackerId);
            if (frame.Phase != MovePhase.Active || frame.MoveInstanceId != contact.MoveInstanceId)
                _contactsToPrune.Add(contact);
        }
        for (int i = 0; i < _contactsToPrune.Count; i++)
            _consumedContacts.Remove(_contactsToPrune[i]);
    }

    private void AdvanceTrajectories()
    {
        if (_stateMachine is null || _trajectories.Count == 0)
            return;
        Span<int> ids = stackalloc int[2];
        int idCount = 0;
        foreach (var pair in _trajectories)
            ids[idCount++] = pair.Key;
        Span<int> completed = stackalloc int[2];
        int completedCount = 0;
        for (int index = 0; index < idCount; index++)
        {
            int playerId = ids[index];
            var current = _trajectories[playerId];
            var response = _stateMachine.GetEffectivePhysicsProfile(playerId);
            var advanced = ForceCalculator.Advance(current, response);
            _trajectories[playerId] = advanced;
            float effectiveFriction = advanced.Profile.Friction *
                (current.Airborne ? response.AirFriction : response.Friction);
            PublishTrajectory(playerId, advanced,
                advanced.Completed ? KnockbackPhase.Completed : KnockbackPhase.Progressed,
                effectiveFriction, response.GravityScale);
            if (advanced.Completed)
                completed[completedCount++] = playerId;
        }
        for (int i = 0; i < completedCount; i++)
            _trajectories.Remove(completed[i]);
    }

    private ulong ReserveGeneration(int playerId)
    {
        _generationCounters.TryGetValue(playerId, out ulong current);
        if (current == ulong.MaxValue)
            throw new InvalidOperationException($"[Physics] Knockback generation exhausted for P{playerId}.");
        ulong next = current + 1;
        _generationCounters[playerId] = next;
        return next;
    }

    internal void SetGenerationForTesting(int playerId, ulong generation) =>
        _generationCounters[playerId] = generation;

    internal PhysicsRuntimeSnapshot CaptureRuntimeSnapshot()
    {
        var participants = new Dictionary<int, PhysicsParticipantSnapshot>();
        var motions = new Dictionary<int, PhysicsMotionSnapshot>();
        foreach (var pair in _participants)
        {
            participants[pair.Key] = pair.Value.CapturePhysicsSnapshot();
            motions[pair.Key] = pair.Value.CaptureMotionSnapshot();
        }
        var trajectories = _trajectories.ToDictionary(
            pair => pair.Key, pair => ToSnapshot(pair.Value));
        return new PhysicsRuntimeSnapshot(new Dictionary<int, ulong>(_generationCounters), participants, motions,
            new HashSet<PhysicsConsumedContact>(_consumedContacts), trajectories);
    }

    internal PhysicsRuntimeSnapshot PrepareRuntimeSnapshot(PhysicsRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.GenerationHighWater is null || snapshot.Participants is null || snapshot.Motions is null ||
            snapshot.ConsumedContacts is null || snapshot.Trajectories is null)
            throw new SnapshotPrepareException(SnapshotParticipantCatalog.PhysicsMotion, "Required physics state is null.");
        foreach (var pair in snapshot.Participants)
        {
            if (!_participants.TryGetValue(pair.Key, out IPhysicsParticipant? live) ||
                live is not IRestorablePhysicsParticipant || pair.Value.PlayerId != pair.Key ||
                !snapshot.Motions.ContainsKey(pair.Key))
                throw new SnapshotPrepareException(SnapshotParticipantCatalog.PhysicsMotion,
                    $"Restorable participant P{pair.Key} is not bound.");
        }
        if (_participants.Keys.Any(id => !snapshot.Participants.ContainsKey(id)))
            throw new SnapshotPrepareException(SnapshotParticipantCatalog.PhysicsMotion,
                "Snapshot omits a bound physics participant.");
        var trajectories = new Dictionary<int, PhysicsTrajectorySnapshot>();
        foreach (var pair in snapshot.Trajectories)
        {
            if (!snapshot.Participants.ContainsKey(pair.Key) || pair.Value.Profile is null ||
                pair.Value.GenerationId == 0 || pair.Value.ContactFrame < 0 ||
                !float.IsFinite(pair.Value.PositionX) || !float.IsFinite(pair.Value.PositionY) ||
                !float.IsFinite(pair.Value.VelocityX) || !float.IsFinite(pair.Value.VelocityY) ||
                !float.IsFinite(pair.Value.GroundY))
                throw new SnapshotPrepareException(SnapshotParticipantCatalog.PhysicsMotion,
                    $"Trajectory for P{pair.Key} is invalid.");
            trajectories[pair.Key] = pair.Value with { Profile = Snapshot.Of(pair.Value.Profile) };
        }
        return new PhysicsRuntimeSnapshot(
            new Dictionary<int, ulong>(snapshot.GenerationHighWater),
            new Dictionary<int, PhysicsParticipantSnapshot>(snapshot.Participants),
            new Dictionary<int, PhysicsMotionSnapshot>(snapshot.Motions),
            new HashSet<PhysicsConsumedContact>(snapshot.ConsumedContacts),
            trajectories);
    }

    internal void InstallRuntimeSnapshot(PhysicsRuntimeSnapshot snapshot)
    {
        foreach (var pair in snapshot.Participants)
            ((IRestorablePhysicsParticipant)_participants[pair.Key]).RestoreRuntimeSnapshot(
                pair.Value, snapshot.Motions[pair.Key]);
        _generationCounters = snapshot.GenerationHighWater;
        _consumedContacts = snapshot.ConsumedContacts;
        _trajectories.Clear();
        foreach (var pair in snapshot.Trajectories)
            _trajectories[pair.Key] = FromSnapshot(pair.Value);
        _launchCandidates.Clear();
        _contexts.Clear();
        _previousActive.Clear();
        _currentActive.Clear();
        _pendingCollisionEvents.Clear();
    }

    private static void PublishTrajectory(int playerId, in TrajectoryState trajectory,
        KnockbackPhase phase, float? effectiveFriction = null, float gravityScale = 1f)
    {
        EventBus.Instance.Publish(new KnockbackAppliedEvent(
            playerId, trajectory.VelocityX, trajectory.VelocityY,
            trajectory.Profile.Gravity * gravityScale,
            effectiveFriction ?? trajectory.Profile.Friction,
            trajectory.PositionX, trajectory.PositionY, trajectory.GenerationId,
            trajectory.ContactFrame, EventBus.Instance.CurrentFrame, phase));
    }

    private static CollisionFrameDefinition? FindFrame(
        IReadOnlyList<CollisionFrameDefinition> frames, int authoredFrame)
    {
        for (int i = 0; i < frames.Count; i++)
            if (frames[i].Frame == authoredFrame)
                return frames[i];
        return null;
    }

    private static void ValidateSnapshotIdentity(
        IPhysicsParticipant participant, in PhysicsParticipantSnapshot snapshot)
    {
        if (snapshot.PlayerId != participant.PlayerId)
            throw new InvalidOperationException(
                $"[Physics] Participant P{participant.PlayerId} returned snapshot for P{snapshot.PlayerId}.");
        if (!float.IsFinite(snapshot.WorldX) || !float.IsFinite(snapshot.WorldY))
            throw new InvalidOperationException(
                $"[Physics] Participant P{participant.PlayerId} returned non-finite world position.");
    }

    private static PhysicsTrajectorySnapshot ToSnapshot(in TrajectoryState trajectory) => new(
        trajectory.GenerationId, trajectory.PositionX, trajectory.PositionY,
        trajectory.VelocityX, trajectory.VelocityY, trajectory.GroundY,
        trajectory.Airborne, Snapshot.Of(trajectory.Profile), trajectory.ContactFrame,
        trajectory.Completed);

    private static TrajectoryState FromSnapshot(in PhysicsTrajectorySnapshot trajectory) => new(
        trajectory.GenerationId, trajectory.PositionX, trajectory.PositionY,
        trajectory.VelocityX, trajectory.VelocityY, trajectory.GroundY,
        trajectory.Airborne, Snapshot.Of(trajectory.Profile), trajectory.ContactFrame,
        trajectory.Completed);

    internal static bool IsBlocking(
        DirectionValue direction, float attackerX, float defenderX,
        CharacterState defenderState = CharacterState.Idle, bool airborne = false)
    {
        if (defenderX == attackerX || airborne ||
            defenderState is not (CharacterState.Idle or CharacterState.Walk or CharacterState.Crouch))
            return false;
        int raw = (int)direction;
        return raw is 1 or 4 or 7;
    }
}
