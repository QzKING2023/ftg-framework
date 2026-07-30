#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.Engine.StateMachine;

internal sealed class StateMachine : IModule, IStateMachine
{
    private readonly IDataStore _dataStore;
    private readonly Dictionary<int, List<CharacterState>> _stacks = new();
    private readonly Dictionary<CharacterState, string> _stateProfiles = new();
    private readonly Dictionary<CharacterState, HashSet<CharacterState>> _allowedTransitions = new();
    private bool _initialized;

    public StateMachine(IDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        _dataStore = dataStore;
        InitializeTransitionTable();
    }

    public void Initialize(IDataStore dataStore)
    {
        if (_initialized) return;
        _initialized = true;

        EventBus.Instance.Subscribe<MoveStartedEvent>(OnMoveStarted);
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(OnMoveFrameChanged);
        EventBus.Instance.Subscribe<HitConnectedEvent>(OnHitConnected);
        EventBus.Instance.Subscribe<MoveBlockedEvent>(OnMoveBlocked);
        EventBus.Instance.Subscribe<MoveCanceledEvent>(OnMoveCanceled);
        EventBus.Instance.Subscribe<ComboEndedEvent>(OnComboEnded);

        FrameworkLog.Info?.Invoke("[StateMachine] StateMachine initialized.");
    }

    public void Shutdown()
    {
        EventBus.Instance.Unsubscribe<MoveStartedEvent>(OnMoveStarted);
        EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(OnMoveFrameChanged);
        EventBus.Instance.Unsubscribe<HitConnectedEvent>(OnHitConnected);
        EventBus.Instance.Unsubscribe<MoveBlockedEvent>(OnMoveBlocked);
        EventBus.Instance.Unsubscribe<MoveCanceledEvent>(OnMoveCanceled);
        EventBus.Instance.Unsubscribe<ComboEndedEvent>(OnComboEnded);

        _stacks.Clear();
        _stateProfiles.Clear();
        _initialized = false;
    }

    // ── IStateMachine public API ──

    public CharacterState GetCurrentState(int playerId)
    {
        ValidatePlayerId(playerId);
        var stack = GetStackInternal(playerId);
        return stack.Count > 0 ? stack[^1] : CharacterState.Idle;
    }

    public IReadOnlyList<CharacterState> GetStack(int playerId)
    {
        ValidatePlayerId(playerId);
        return GetStackInternal(playerId).ToList();
    }

    public int GetStackDepth(int playerId)
    {
        ValidatePlayerId(playerId);
        return _stacks.TryGetValue(playerId, out var stack) ? stack.Count : 0;
    }

    public void InitializePlayer(int playerId)
    {
        ValidatePlayerId(playerId);

        if (_stacks.TryGetValue(playerId, out var existing) && existing.Count > 0)
        {
            FrameworkLog.Error?.Invoke($"[StateMachine] InitializePlayer called for player {playerId} but stack already exists with {existing.Count} entries.");
            return;
        }

        _stacks[playerId] = new List<CharacterState> { CharacterState.Idle };
    }

    public void PushState(int playerId, CharacterState state)
    {
        if (!_initialized)
        {
            FrameworkLog.Error?.Invoke("[StateMachine] PushState called before Initialize.");
            return;
        }

        ValidatePlayerId(playerId);

        if (state == CharacterState.Idle)
        {
            FrameworkLog.Error?.Invoke("[StateMachine] Cannot push Idle — use InitializePlayer for initial state.");
            return;
        }

        var stack = GetOrCreateStack(playerId);
        var current = stack.Count > 0 ? stack[^1] : CharacterState.Idle;

        if (!IsTransitionAllowed(current, state))
        {
            FrameworkLog.Error?.Invoke($"[StateMachine] Invalid transition from {current} to {state} for player {playerId}.");
            return;
        }

        PushStateInternal(playerId, stack, state);
    }

    public void PopState(int playerId)
    {
        if (!_initialized)
        {
            FrameworkLog.Error?.Invoke("[StateMachine] PopState called before Initialize.");
            return;
        }

        ValidatePlayerId(playerId);

        if (!_stacks.TryGetValue(playerId, out var stack) || stack.Count == 0)
        {
            FrameworkLog.Error?.Invoke($"[StateMachine] Cannot pop — stack is empty for player {playerId}.");
            return;
        }

        if (stack.Count == 1 && stack[0] == CharacterState.Idle)
        {
            FrameworkLog.Error?.Invoke($"[StateMachine] Cannot pop Idle — only state remaining for player {playerId}.");
            return;
        }

        var oldStack = stack.ToArray();
        stack.RemoveAt(stack.Count - 1);
        PublishEvents(playerId, oldStack, stack.ToArray());
    }

    public void ReplaceState(int playerId, CharacterState newState)
    {
        if (!_initialized)
        {
            FrameworkLog.Error?.Invoke("[StateMachine] ReplaceState called before Initialize.");
            return;
        }

        ValidatePlayerId(playerId);

        var stack = GetOrCreateStack(playerId);
        var oldStack = stack.ToArray();

        if (stack.Count > 0 && stack[^1] != CharacterState.Idle)
            stack.RemoveAt(stack.Count - 1);

        stack.Add(newState);
        PublishEvents(playerId, oldStack, stack.ToArray());
    }

    public PhysicsResponseProfile GetEffectivePhysicsProfile(int playerId)
    {
        ValidatePlayerId(playerId);
        var stack = GetStackInternal(playerId);

        float knockbackMultiplier = 1.0f;
        float gravityScale = 1.0f;
        float friction = 0.5f;
        float airFriction = 0.2f;
        bool? participatesInHitstop = null;

        // Walk top-down: top of stack has highest priority.
        // Each field is only set by the FIRST non-default value encountered (top wins).
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            var state = stack[i];
            if (!_stateProfiles.TryGetValue(state, out var profileId))
                continue;

            var profile = _dataStore.GetPhysicsResponseProfile(profileId);
            if (profile is null)
            {
                FrameworkLog.Error?.Invoke($"[StateMachine] PhysicsResponseProfile '{profileId}' not found for state {state}.");
                continue;
            }

            var snapshotted = Snapshot.Of(profile);

            if (knockbackMultiplier == 1.0f && snapshotted.KnockbackMultiplier != 1.0f)
                knockbackMultiplier = snapshotted.KnockbackMultiplier;
            if (gravityScale == 1.0f && snapshotted.GravityScale != 1.0f)
                gravityScale = snapshotted.GravityScale;
            if (friction == 0.5f && snapshotted.Friction != 0.5f)
                friction = snapshotted.Friction;
            if (airFriction == 0.2f && snapshotted.AirFriction != 0.2f)
                airFriction = snapshotted.AirFriction;
            if (participatesInHitstop is null && snapshotted.ParticipatesInHitstop != false)
                participatesInHitstop = snapshotted.ParticipatesInHitstop;
        }

        return new PhysicsResponseProfile
        {
            ProfileId = "effective",
            KnockbackMultiplier = knockbackMultiplier,
            GravityScale = gravityScale,
            Friction = friction,
            AirFriction = airFriction,
            ParticipatesInHitstop = participatesInHitstop ?? true
        };
    }

    public void RegisterStateProfile(CharacterState state, string physicsResponseProfileId)
    {
        ArgumentNullException.ThrowIfNull(physicsResponseProfileId);

        _stateProfiles[state] = physicsResponseProfileId;

        if (_dataStore.GetPhysicsResponseProfile(physicsResponseProfileId) is null)
            FrameworkLog.Error?.Invoke($"[StateMachine] PhysicsResponseProfile '{physicsResponseProfileId}' not in DataStore for state {state}.");
    }

    // ── Event handlers (AD-11 peer model) ──

    private void OnMoveStarted(MoveStartedEvent e)
    {
        // Bypass transition guard — MoveStarted is authoritative, already validated
        // by GameLoop (GetPhase == Idle) or injected by replay with explicit intent.
        var stack = GetOrCreateStack(e.PlayerId);
        PushStateInternal(e.PlayerId, stack, CharacterState.AttackStartup);
    }

    private void OnMoveFrameChanged(MoveFrameChangedEvent e)
    {
        switch (e.Phase)
        {
            case MovePhase.Active:
                ReplaceState(e.PlayerId, CharacterState.AttackActive);
                break;
            case MovePhase.Recovery:
                ReplaceState(e.PlayerId, CharacterState.AttackRecovery);
                break;
            case MovePhase.Idle:
                ResetToIdle(e.PlayerId);
                break;
        }
    }

    private void OnHitConnected(HitConnectedEvent e)
    {
        ReplaceState(e.DefenderId, CharacterState.Hitstun);
    }

    private void OnMoveBlocked(MoveBlockedEvent e)
    {
        ReplaceState(e.DefenderId, CharacterState.Blockstun);
    }

    private void OnMoveCanceled(MoveCanceledEvent e)
    {
        PopAttackStates(e.PlayerId);
    }

    private void OnComboEnded(ComboEndedEvent e)
    {
        ResetToIdle(e.PlayerId);
    }

    // ── Internals ──

    private void PushStateInternal(int playerId, List<CharacterState> stack, CharacterState state)
    {
        var oldStack = stack.ToArray();
        stack.Add(state);
        PublishEvents(playerId, oldStack, stack.ToArray());
    }

    private void PopAttackStates(int playerId)
    {
        if (!_stacks.TryGetValue(playerId, out var stack) || stack.Count == 0)
            return;

        var oldStack = stack.ToArray();
        bool changed = false;

        while (stack.Count > 1)
        {
            var top = stack[^1];
            if (top is CharacterState.AttackStartup or CharacterState.AttackActive or CharacterState.AttackRecovery)
            {
                stack.RemoveAt(stack.Count - 1);
                changed = true;
            }
            else
            {
                break;
            }
        }

        if (changed)
            PublishEvents(playerId, oldStack, stack.ToArray());
    }

    private void ResetToIdle(int playerId)
    {
        if (!_stacks.TryGetValue(playerId, out var stack) || stack.Count <= 1)
            return;

        var oldStack = stack.ToArray();
        stack.RemoveRange(1, stack.Count - 1);
        PublishEvents(playerId, oldStack, stack.ToArray());
    }

    private List<CharacterState> GetStackInternal(int playerId)
    {
        return _stacks.TryGetValue(playerId, out var stack) ? stack : new List<CharacterState>();
    }

    private List<CharacterState> GetOrCreateStack(int playerId)
    {
        if (!_stacks.TryGetValue(playerId, out var stack))
        {
            stack = new List<CharacterState> { CharacterState.Idle };
            _stacks[playerId] = stack;
        }
        return stack;
    }

    private void PublishEvents(int playerId, CharacterState[] oldStack, CharacterState[] newStack)
    {
        EventBus.Instance.Publish(new StateChangedEvent(playerId, oldStack, newStack));
        EventBus.Instance.Publish(new StateStackChangedEvent(playerId, newStack));
    }

    private static void ValidatePlayerId(int playerId)
    {
        if (playerId < 1 || playerId > 2)
            throw new ArgumentOutOfRangeException(nameof(playerId), $"[StateMachine] Invalid playerId: {playerId}. Must be 1 or 2.");
    }

    private bool IsTransitionAllowed(CharacterState from, CharacterState to)
    {
        return _allowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    internal void SetAllowedTransition(CharacterState from, CharacterState to, bool allowed)
    {
        if (!_allowedTransitions.ContainsKey(from))
            _allowedTransitions[from] = new HashSet<CharacterState>();

        if (allowed)
            _allowedTransitions[from].Add(to);
        else
            _allowedTransitions[from].Remove(to);
    }

    private void InitializeTransitionTable()
    {
        Allow(CharacterState.Idle, CharacterState.Walk, CharacterState.Crouch, CharacterState.JumpStartup, CharacterState.AttackStartup);
        Allow(CharacterState.Walk, CharacterState.Idle, CharacterState.Crouch, CharacterState.JumpStartup, CharacterState.AttackStartup);
        Allow(CharacterState.Crouch, CharacterState.Idle, CharacterState.Walk, CharacterState.AttackStartup);
        Allow(CharacterState.JumpStartup, CharacterState.JumpActive);
        Allow(CharacterState.JumpActive, CharacterState.JumpRecovery, CharacterState.AttackStartup, CharacterState.Airborne);
        Allow(CharacterState.JumpRecovery, CharacterState.Idle);
        Allow(CharacterState.AttackStartup, CharacterState.AttackActive);
        Allow(CharacterState.AttackActive, CharacterState.AttackRecovery);
        Allow(CharacterState.AttackRecovery, CharacterState.Idle);
        Allow(CharacterState.Hitstun, CharacterState.Knockdown, CharacterState.Airborne);
        Allow(CharacterState.Blockstun, CharacterState.Idle, CharacterState.AttackStartup);
        Allow(CharacterState.Knockdown, CharacterState.Wakeup);
        Allow(CharacterState.Wakeup, CharacterState.Idle);
        Allow(CharacterState.Airborne, CharacterState.AttackStartup, CharacterState.Hitstun, CharacterState.JumpRecovery, CharacterState.Idle);
    }

    private void Allow(CharacterState from, params CharacterState[] toStates)
    {
        if (!_allowedTransitions.ContainsKey(from))
            _allowedTransitions[from] = new HashSet<CharacterState>();
        foreach (var to in toStates)
            _allowedTransitions[from].Add(to);
    }
}
