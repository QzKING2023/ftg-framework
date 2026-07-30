#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using Godot;

namespace FTG_Framework.Characters;

public partial class CharacterController : Node2D
{
    [Export] public Node2D? SpriteContainer { get; set; }
    [Export] public Node2D? HurtboxContainer { get; set; }
    [Export] public AnimationPlayer? AnimationPlayer { get; set; }
    [Export] public int PlayerId { get; set; } = 1;
    [Export] public string CharacterId { get; set; } = "";
    [Export] public Label? StateDebugLabel { get; set; }

    private CharacterViewModel _viewModel = null!;
    private readonly List<Area2D> _hurtboxes = new();
    private bool _facingRight = true;
    private System.Action<StateChangedEvent>? _stateChangedHandler;
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

        CollectHurtboxes();

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
    }

    public IReadOnlyList<Area2D> GetHurtboxes() => _hurtboxes;

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
            : fallbackEvent is { NewStack.Length: > 0 } e
                ? e.NewStack[^1]
                : CharacterState.Idle;
        if (_displayedState == top)
            return;

        _displayedState = top;
        var animName = _viewModel.GetAnimationName(top);
        AnimationPlayer?.Play(animName);

        UpdateFacing();

        if (StateDebugLabel is not null)
            StateDebugLabel.Text = $"[P{PlayerId}] {top}";
    }

    internal static string GetAuthoritativeStateLabel(IStateMachine stateMachine, int playerId)
    {
        ArgumentNullException.ThrowIfNull(stateMachine);
        return $"[P{playerId}] {stateMachine.GetCurrentState(playerId)}";
    }

    private void UpdateFacing()
    {
        if (_gameLoop is null)
            return;

        var history = _gameLoop.InputHistory?.GetDirectionalHistory(PlayerId);
        if (history is not { Count: > 0 })
            return;

        var raw = history[^1].Value;

        var dir = raw is >= 1 and <= 9
            ? (DirectionValue)raw
            : DirectionValue.Neutral;

        var facing = _viewModel.ShouldFaceRight(dir);
        if (facing.HasValue)
            _facingRight = facing.Value;

        if (SpriteContainer is not null)
            SpriteContainer.Scale = SpriteContainer.Scale with { X = _facingRight ? 1.0f : -1.0f };
    }

    private void CollectHurtboxes()
    {
        _hurtboxes.Clear();
        if (HurtboxContainer is null)
            return;

        foreach (var child in HurtboxContainer.GetChildren())
        {
            if (child is Area2D area)
            {
                area.Monitoring = false;
                area.Monitorable = true;
                _hurtboxes.Add(area);
            }
        }
    }
}
