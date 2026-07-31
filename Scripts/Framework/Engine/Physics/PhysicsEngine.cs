#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.Physics;

internal sealed class PhysicsEngine : IPhysicsEngine
{
    private readonly IDataStore _dataStore;
    private readonly IFrameDataEngine _frameData;
    private readonly IStateMachine? _stateMachine;
    private readonly SortedDictionary<int, IPhysicsParticipant> _participants = new();
    private readonly HashSet<HitKey> _previousActive = new();
    private readonly HashSet<HitKey> _currentActive = new();
    private readonly Dictionary<ContextKey, HitContext> _contexts = new();
    private readonly Dictionary<int, TrajectoryState> _trajectories = new();
    private readonly Dictionary<int, TrajectoryState> _launchCandidates = new();
    private long _nextGeneration;
    private bool _initialized;

    private readonly record struct HitKey(
        long MoveInstanceId, int AttackerId, int DefenderId, string HitboxId);
    private readonly record struct ContextKey(
        int AttackerId, int DefenderId, string HitboxId, int ContactFrame);

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
        _contexts.Clear();
        _trajectories.Clear();
        _launchCandidates.Clear();
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
    }

    public bool TryGetHitContext(
        int attackerId, int defenderId, string hitboxId, int contactFrame,
        out HitContext context) =>
        _contexts.TryGetValue(new ContextKey(attackerId, defenderId, hitboxId, contactFrame), out context);

    public void Update()
    {
        if (!_initialized)
            return;
        _contexts.Clear();
        _currentActive.Clear();
        _launchCandidates.Clear();
        if (_participants.Count == 2)
        {
            var firstParticipant = _participants[1];
            var secondParticipant = _participants[2];
            var first = firstParticipant.CapturePhysicsSnapshot();
            var second = secondParticipant.CapturePhysicsSnapshot();
            ValidateSnapshotIdentity(firstParticipant, first);
            ValidateSnapshotIdentity(secondParticipant, second);
            Detect(first, second);
            Detect(second, first);
        }
        foreach (var candidate in _launchCandidates)
            _trajectories[candidate.Key] = candidate.Value;
        AdvanceTrajectories();
        _previousActive.Clear();
        foreach (var key in _currentActive)
            _previousActive.Add(key);
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

            var hitKey = new HitKey(
                evaluated.MoveInstanceId, attacker.PlayerId, defender.PlayerId, hitbox.BoxId);
            _currentActive.Add(hitKey);
            if (_previousActive.Contains(hitKey))
                continue;

            if (IsBlocking(defender.Direction, attacker.WorldX, defender.WorldX))
            {
                EventBus.Instance.Publish(new MoveBlockedEvent(
                    attacker.PlayerId, defender.PlayerId, move.MoveId,
                    move.BlockAdvantage, move.Damage, contactFrame));
                continue;
            }

            var hitEvent = new HitConnectedEvent(
                attacker.PlayerId, defender.PlayerId, move.MoveId,
                move.HitAdvantage, move.Damage, contactFrame, hitbox.BoxId);
            EventBus.Instance.Publish(hitEvent);
            KnockbackProfile? profile = move.KnockbackProfileId is null
                ? null
                : _dataStore.GetKnockbackProfile(move.KnockbackProfileId);
            if (profile is not null)
                profile = Snapshot.Of(profile);
            var context = new HitContext(hitEvent, hitbox.BoxId, profile);
            _contexts[new ContextKey(
                attacker.PlayerId, defender.PlayerId, hitbox.BoxId, contactFrame)] = context;
            if (profile is not null && _stateMachine is not null &&
                _participants.TryGetValue(defender.PlayerId, out var participant))
            {
                var response = _stateMachine.GetEffectivePhysicsProfile(defender.PlayerId);
                var motion = participant.CaptureMotionSnapshot();
                _launchCandidates[defender.PlayerId] = ForceCalculator.Launch(
                    ++_nextGeneration,
                    defender.WorldX,
                    defender.WorldY,
                    attacker.WorldX,
                    attacker.FacingRight,
                    motion,
                    profile,
                    response,
                    contactFrame);
            }
        }
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
            EventBus.Instance.Publish(new KnockbackAppliedEvent(
                playerId,
                advanced.VelocityX,
                advanced.VelocityY,
                advanced.Profile.Gravity * response.GravityScale,
                effectiveFriction,
                advanced.PositionX,
                advanced.PositionY,
                advanced.GenerationId,
                advanced.ContactFrame,
                EventBus.Instance.CurrentFrame,
                advanced.Completed));
            if (advanced.Completed)
                completed[completedCount++] = playerId;
        }
        for (int i = 0; i < completedCount; i++)
            _trajectories.Remove(completed[i]);
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
    }

    internal static bool IsBlocking(DirectionValue direction, float attackerX, float defenderX)
    {
        if (defenderX == attackerX)
            return false;
        int raw = (int)direction;
        bool holdsLeft = raw is 1 or 4 or 7;
        bool holdsRight = raw is 3 or 6 or 9;
        return defenderX < attackerX ? holdsLeft : holdsRight;
    }
}
