#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using Godot;

namespace FTG_Framework.Characters;

public partial class CharacterController : Node2D, IPhysicsParticipant, IRestorablePhysicsParticipant
{
    [Export] public Node2D? SpriteContainer { get; set; }
    [Export] public Node2D? HurtboxContainer { get; set; }
    [Export] public AnimationPlayer? AnimationPlayer { get; set; }
    [Export] public int PlayerId { get; set; } = 1;
    [Export] public string CharacterId { get; set; } = "";
    [Export] public Label? StateDebugLabel { get; set; }

    private CharacterViewModel _viewModel = null!;
    private readonly List<Area2D> _hurtboxes = new();
    private readonly List<CollisionBoxDefinition> _neutralHurtboxes = new();
    private bool _facingRight = true;
    private System.Action<StateChangedEvent>? _stateChangedHandler;
    private System.Action<KnockbackAppliedEvent>? _knockbackHandler;
    private System.Action<ReplayStartedEvent>? _replayStartedHandler;
    private System.Action<ReplayEndedEvent>? _replayEndedHandler;
    private System.Action<MatchInitializedEvent>? _matchInitializedHandler;
    private PhysicsMotionSnapshot _motion;
    private ulong _latestKnockbackGeneration;
    private int _latestKnockbackFrame = -1;
    private KnockbackAppliedEvent? _lastKnockbackEvent;
    private GameLoop? _gameLoop;
    private CharacterState? _displayedState;

    public override void _Ready()
    {
        _viewModel = new CharacterViewModel();

        if (SpriteContainer is null)
            GD.PushWarning($"[CharacterController] P{PlayerId}: SpriteContainer is not assigned.");
        if (HurtboxContainer is null)
            GD.PushWarning($"[CharacterController] P{PlayerId}: HurtboxContainer is not assigned.");
        if (AnimationPlayer is null)
            GD.PushWarning($"[CharacterController] P{PlayerId}: AnimationPlayer is not assigned.");

        _gameLoop = GetNode<GameLoop>("/root/GameLoop");
        if (_gameLoop is null)
        {
            GD.PushError($"[CharacterController] P{PlayerId}: GameLoop autoload not found.");
            return;
        }

        if (PlayerId < 1 || PlayerId > 2)
        {
            GD.PushError($"[CharacterController] P{PlayerId}: PlayerId must be 1 or 2.");
            return;
        }

        _facingRight = PlayerId == 1;

        _gameLoop.StateMachine?.InitializePlayer(PlayerId);

        _stateChangedHandler = OnStateChanged;
        EventBus.Instance.Subscribe(_stateChangedHandler);
        _knockbackHandler = OnKnockbackApplied;
        EventBus.Instance.Subscribe(_knockbackHandler);
        _replayStartedHandler = _ => ResetKnockbackEventOrder();
        _replayEndedHandler = _ => ResetKnockbackEventOrder();
        _matchInitializedHandler = _ => ResetKnockbackEventOrder();
        EventBus.Instance.Subscribe(_replayStartedHandler);
        EventBus.Instance.Subscribe(_replayEndedHandler);
        EventBus.Instance.Subscribe(_matchInitializedHandler);
        _motion = new PhysicsMotionSnapshot(0, 0, false, GlobalPosition.Y);

        CollectHurtboxes();
        _gameLoop.PhysicsEngine?.Register(this);

        ApplyAuthoritativeState();
    }

    public override void _Process(double delta)
    {
        ApplyAuthoritativeState();
    }

    public override void _ExitTree()
    {
        if (_stateChangedHandler is not null)
            EventBus.Instance.Unsubscribe(_stateChangedHandler);
        _stateChangedHandler = null;
        if (_knockbackHandler is not null)
            EventBus.Instance.Unsubscribe(_knockbackHandler);
        _knockbackHandler = null;
        if (_replayStartedHandler is not null)
            EventBus.Instance.Unsubscribe(_replayStartedHandler);
        if (_replayEndedHandler is not null)
            EventBus.Instance.Unsubscribe(_replayEndedHandler);
        if (_matchInitializedHandler is not null)
            EventBus.Instance.Unsubscribe(_matchInitializedHandler);
        _replayStartedHandler = null;
        _replayEndedHandler = null;
        _matchInitializedHandler = null;
        _gameLoop?.PhysicsEngine?.Unregister(this);
    }

    public IReadOnlyList<Area2D> GetHurtboxes() => _hurtboxes;

    public PhysicsParticipantSnapshot CapturePhysicsSnapshot()
    {
        DirectionValue direction = DirectionValue.Neutral;
        var history = _gameLoop?.InputHistory?.GetDirectionalHistory(PlayerId);
        if (history is { Count: > 0 } && Enum.IsDefined(typeof(DirectionValue), history[^1].Value))
            direction = (DirectionValue)history[^1].Value;
        return new PhysicsParticipantSnapshot(
            PlayerId,
            string.IsNullOrWhiteSpace(CharacterId) ? $"player-{PlayerId}" : CharacterId,
            GlobalPosition.X,
            GlobalPosition.Y,
            direction,
            _facingRight,
            _neutralHurtboxes);
    }

    public PhysicsMotionSnapshot CaptureMotionSnapshot()
    {
        if (!_motion.Airborne)
            _motion = _motion with { GroundY = GlobalPosition.Y };
        return _motion;
    }

    public void ApplyPhysicsState(PhysicsParticipantSnapshot participant, PhysicsMotionSnapshot motion)
    {
        GlobalPosition = new Vector2(participant.WorldX, participant.WorldY);
        _facingRight = participant.FacingRight;
        _motion = motion;
        if (SpriteContainer is not null)
            SpriteContainer.Scale = SpriteContainer.Scale with { X = _facingRight ? 1.0f : -1.0f };
    }

    void IRestorablePhysicsParticipant.RestoreRuntimeSnapshot(
        PhysicsParticipantSnapshot participant, PhysicsMotionSnapshot motion)
    {
        ApplyPhysicsState(participant, motion);
        ResetKnockbackEventOrder();
    }

    private void OnKnockbackApplied(KnockbackAppliedEvent e)
    {
        if (e.PlayerId != PlayerId || !e.WorldX.HasValue || !e.WorldY.HasValue)
            return;
        if (!ShouldApplyKnockbackEvent(e, _lastKnockbackEvent))
            return;
        _latestKnockbackGeneration = e.GenerationId;
        _latestKnockbackFrame = e.FrameNumber;
        GlobalPosition = new Vector2(e.WorldX.Value, e.WorldY.Value);
        _motion = new PhysicsMotionSnapshot(
            e.HorizontalForce, e.VerticalForce, e.Phase != KnockbackPhase.Completed && e.WorldY.Value < _motion.GroundY,
            _motion.GroundY);
        _lastKnockbackEvent = e;
    }

    internal static bool ShouldApplyKnockbackEvent(
        ulong generationId, int frameNumber, ulong latestGeneration, int latestFrame) =>
        generationId > 0 &&
        (generationId > latestGeneration ||
         generationId == latestGeneration && frameNumber >= latestFrame);

    internal static bool ShouldApplyKnockbackEvent(
        KnockbackAppliedEvent next, KnockbackAppliedEvent? last)
    {
        if (next.GenerationId == 0 || next.Phase == 0) return false;
        if (last is null) return next.Phase == KnockbackPhase.Started;
        var prior = last.Value;
        if (next.Equals(prior)) return false;
        if (next.GenerationId > prior.GenerationId)
            return next.Phase == KnockbackPhase.Started &&
                next.ContactFrame >= prior.ContactFrame && next.FrameNumber >= prior.FrameNumber;
        if (next.GenerationId < prior.GenerationId || next.ContactFrame != prior.ContactFrame ||
            next.FrameNumber <= prior.FrameNumber || prior.Phase == KnockbackPhase.Completed)
            return false;
        return next.Phase == KnockbackPhase.Completed ||
               next.Phase == prior.Phase + 1 ||
               prior.Phase == KnockbackPhase.Progressed && next.Phase == KnockbackPhase.Progressed;
    }

    private void ResetKnockbackEventOrder()
    {
        _latestKnockbackGeneration = 0;
        _latestKnockbackFrame = -1;
        _lastKnockbackEvent = null;
    }

    private void OnStateChanged(StateChangedEvent e)
    {
        if (e.PlayerId != PlayerId)
            return;

        ApplyAuthoritativeState(e);
    }

    private void ApplyAuthoritativeState(StateChangedEvent? fallbackEvent = null)
    {
        var top = _gameLoop?.StateMachine is { } stateMachine
            ? stateMachine.GetCurrentState(PlayerId)
            : fallbackEvent is { } e
                ? e.NewSnapshot.TopOrIdle
                : CharacterState.Idle;
        if (_displayedState == top)
            return;

        _displayedState = top;
        var animName = _viewModel.GetAnimationName(top);
        if (AnimationPlayer?.HasAnimation(animName) == true)
            AnimationPlayer.Play(animName);

        if (StateDebugLabel is not null)
            StateDebugLabel.Text = $"[P{PlayerId}] {top}";
    }

    internal static string GetAuthoritativeStateLabel(IStateMachine stateMachine, int playerId)
    {
        ArgumentNullException.ThrowIfNull(stateMachine);
        return $"[P{playerId}] {stateMachine.GetCurrentState(playerId)}";
    }

    private void CollectHurtboxes()
    {
        _hurtboxes.Clear();
        _neutralHurtboxes.Clear();
        var configured = string.IsNullOrWhiteSpace(CharacterId)
            ? null
            : _gameLoop?.DataStore?.GetCharacter(CharacterId);
        if (configured is not null)
            _neutralHurtboxes.AddRange(configured.NeutralHurtboxes);
        if (HurtboxContainer is null)
            return;

        foreach (var child in HurtboxContainer.GetChildren())
        {
            if (child is Area2D area)
            {
                area.Monitoring = false;
                area.Monitorable = true;
                _hurtboxes.Add(area);
                if (configured is null)
                    CollectRectangleShapes(area);
            }
        }
    }

    private void CollectRectangleShapes(Area2D area)
    {
        foreach (var child in area.GetChildren())
        {
            if (child is not CollisionShape2D collision || collision.Disabled)
                continue;
            if (collision.Shape is not RectangleShape2D rectangle)
                throw new InvalidOperationException(
                    $"[Physics] P{PlayerId}: only RectangleShape2D hurtboxes are supported.");
            if (!Godot.Mathf.IsZeroApprox(collision.GlobalRotation))
                throw new InvalidOperationException(
                    $"[Physics] P{PlayerId}: rotated hurtboxes are not supported.");
            var scale = collision.GlobalScale;
            if (scale.X <= 0 || scale.Y <= 0)
                throw new InvalidOperationException(
                    $"[Physics] P{PlayerId}: hurtbox scale must be positive.");
            var offset = collision.GlobalPosition - GlobalPosition;
            _neutralHurtboxes.Add(new CollisionBoxDefinition
            {
                BoxId = $"{area.Name}/{collision.Name}",
                X = offset.X,
                Y = offset.Y,
                Width = rectangle.Size.X * scale.X,
                Height = rectangle.Size.Y * scale.Y
            });
        }
    }
}
