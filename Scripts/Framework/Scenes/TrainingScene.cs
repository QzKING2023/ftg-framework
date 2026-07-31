#nullable enable
using System;
using FTG_Framework.Characters;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.Scenes;

public partial class TrainingScene : Node, IScene
{
    public IDataStore? DataStore { get; set; }
    public IInputHistory? InputHistory { get; set; }
    public IFrameDataEngine? FrameDataEngine { get; set; }
    public IStateMachine? StateMachine { get; set; }
    public string P1CharacterId { get; set; } = string.Empty;
    public string P2CharacterId { get; set; } = string.Empty;

    private PlaybackControls? _playbackControls;
    private HitboxOverlay? _hitboxOverlay;
    private InputLog? _inputLog;
    private bool _overlayEnabled;
    private readonly TrainingStateRecovery _stateRecovery = new();

    public void Enter(ISceneManager manager)
    {
        InstantiateCharacters();

        var frameDataPanel = new FrameDataPanel
        {
            PanelPosition = new Vector2(10, 10),
            DataStore = DataStore
        };
        AddChild(frameDataPanel);

        var advantageDisplay = new AdvantageDisplay
        {
            PanelPosition = new Vector2(10, 75)
        };
        AddChild(advantageDisplay);

        _inputLog = new InputLog
        {
            PanelPosition = new Vector2(10, 110),
            InputHistory = InputHistory
        };
        AddChild(_inputLog);

        _playbackControls = new PlaybackControls
        {
            FrameDataEngine = FrameDataEngine,
            InputLog = _inputLog
        };
        AddChild(_playbackControls);

        _hitboxOverlay = new HitboxOverlay();
        AddChild(_hitboxOverlay);

        var debugPanel = new EventBusDebugPanel();
        AddChild(debugPanel);

        SubscribeDebugEvents();
    }

    private void InstantiateCharacters()
    {
        var templatePath = "res://Characters/character_template.tscn";
        if (!Godot.FileAccess.FileExists(templatePath))
        {
            GD.PushWarning($"[TrainingScene] Character template not found at {templatePath}");
            return;
        }

        var scene = ResourceLoader.Load<PackedScene>(templatePath);
        if (scene is null)
        {
            GD.PushError($"[TrainingScene] Failed to load PackedScene from {templatePath}");
            return;
        }

        var visibleRect = GetViewport().GetVisibleRect();
        if (!TryCalculateCharacterSpawnPositions(visibleRect, out var p1Position, out var p2Position))
        {
            GD.PushWarning(
                $"[TrainingScene] Visible viewport {visibleRect.Size} is unsupported; " +
                "character labels require at least 500x120.");
            return;
        }

        var p1Node = scene.Instantiate();
        if (p1Node is not CharacterController p1)
        {
            GD.PushError("[TrainingScene] Template root node is not CharacterController.");
            p1Node.QueueFree();
            return;
        }

        p1.PlayerId = 1;
        p1.CharacterId = P1CharacterId;
        p1.Position = p1Position;

        var p2Node = scene.Instantiate();
        if (p2Node is not CharacterController p2)
        {
            GD.PushError("[TrainingScene] Template root node is not CharacterController (P2).");
            p2Node.QueueFree();
            p1.Free();
            return;
        }

        p2.PlayerId = 2;
        p2.CharacterId = P2CharacterId;
        p2.Position = p2Position;

        AddChild(p1);
        AddChild(p2);
    }

    internal static bool TryCalculateCharacterSpawnPositions(
        Rect2 visibleRect, out Vector2 p1Position, out Vector2 p2Position)
    {
        const float halfSeparation = 200.0f;
        const float labelHalfWidth = 50.0f;
        const float labelTopOffset = -60.0f;
        const float minimumWidth = 2.0f * (halfSeparation + labelHalfWidth);
        const float minimumHeight = -2.0f * labelTopOffset;

        if (!visibleRect.Position.IsFinite() ||
            !visibleRect.Size.IsFinite() ||
            visibleRect.Size.X < minimumWidth ||
            visibleRect.Size.Y < minimumHeight)
        {
            p1Position = Vector2.Zero;
            p2Position = Vector2.Zero;
            return false;
        }

        var center = visibleRect.Position + visibleRect.Size / 2.0f;
        p1Position = center - new Vector2(halfSeparation, 0);
        p2Position = center + new Vector2(halfSeparation, 0);

        if (!p1Position.IsFinite() ||
            !p2Position.IsFinite() ||
            p2Position.X - p1Position.X != 2.0f * halfSeparation)
        {
            p1Position = Vector2.Zero;
            p2Position = Vector2.Zero;
            return false;
        }

        return true;
    }

    public void Exit()
    {
        if (StateMachine is not null)
            _stateRecovery.RestoreIfOwned(StateMachine);
        UnsubscribeDebugEvents();
    }

    public override void _Process(double delta)
    {
        bool pauseKey = Godot.Input.IsKeyPressed(Key.P);
        bool stepFwd = Godot.Input.IsKeyPressed(Key.Right);
        bool stepBack = Godot.Input.IsKeyPressed(Key.Left);
        bool hitKey = Godot.Input.IsKeyPressed(Key.H);
        bool blockKey = Godot.Input.IsKeyPressed(Key.B);
        bool overlayKey = Godot.Input.IsKeyPressed(Key.O);
        bool showP1Key = Godot.Input.IsKeyPressed(Key.Key1);
        bool showP2Key = Godot.Input.IsKeyPressed(Key.Key2);

        ProcessTestKeys(pauseKey, stepFwd, stepBack, hitKey, blockKey, overlayKey, showP1Key, showP2Key);

        _prevPauseKey = pauseKey;
        _prevStepFwdKey = stepFwd;
        _prevStepBackKey = stepBack;
        _prevHitKey = hitKey;
        _prevBlockKey = blockKey;
        _prevOverlayKey = overlayKey;
        _prevShowP1Key = showP1Key;
        _prevShowP2Key = showP2Key;
    }

    private void ProcessTestKeys(bool pauseKey, bool stepFwd, bool stepBack,
        bool hitKey, bool blockKey, bool overlayKey, bool showP1Key, bool showP2Key)
    {
        if (pauseKey && !_prevPauseKey)
            _playbackControls?.TogglePause();

        if (stepFwd && !_prevStepFwdKey)
            _playbackControls?.StepForward();

        if (stepBack && !_prevStepBackKey)
            _playbackControls?.StepBackward();

        // H/B event injection was removed with FrameDataEngine's legacy hit
        // publisher. Use configured collision geometry to exercise hit/block paths.

        if (overlayKey && !_prevOverlayKey)
        {
            _overlayEnabled = !_overlayEnabled;
            if (_hitboxOverlay is not null)
                _hitboxOverlay.Enabled = _overlayEnabled;
            GD.Print($"[Test] HitboxOverlay Enabled = {_overlayEnabled}");
        }

        if (showP1Key && !_prevShowP1Key && _inputLog is not null)
        {
            _inputLog.ShowP1 = !_inputLog.ShowP1;
            GD.Print($"[Test] InputLog ShowP1 = {_inputLog.ShowP1}");
        }

        if (showP2Key && !_prevShowP2Key && _inputLog is not null)
        {
            _inputLog.ShowP2 = !_inputLog.ShowP2;
            GD.Print($"[Test] InputLog ShowP2 = {_inputLog.ShowP2}");
        }
    }

    // ── Debug event subscriptions ──

    private bool _prevPauseKey, _prevStepFwdKey, _prevStepBackKey;
    private bool _prevHitKey, _prevBlockKey, _prevOverlayKey;
    private bool _prevShowP1Key, _prevShowP2Key;

    private Action<MoveFrameChangedEvent>? _dbgMoveFrame;
    private Action<CancelWindowEnteredEvent>? _dbgCancelEnter;
    private Action<CancelWindowExitedEvent>? _dbgCancelExit;
    private Action<HitConnectedEvent>? _dbgHit;
    private Action<MoveBlockedEvent>? _dbgBlock;
    private Action<InputReceivedEvent>? _dbgInput;
    private Action<ReplayStartedEvent>? _dbgReplayStart;
    private Action<ReplayEndedEvent>? _dbgReplayEnd;
    private Action<FrameAdvancedEvent>? _trainingFrameAdvanced;

    private void SubscribeDebugEvents()
    {
        _dbgMoveFrame ??= e =>
            GD.Print($"[DEBUG] MoveFrame: P{e.PlayerId} {e.MoveId} phase={e.Phase} frame={e.CurrentFrame}/{e.TotalFrames}");
        _dbgCancelEnter ??= e =>
            GD.Print($"[DEBUG] CancelEnter: P{e.PlayerId} {e.MoveId} cat={e.Category} [{e.StartFrame}-{e.EndFrame}]");
        _dbgCancelExit ??= e =>
            GD.Print($"[DEBUG] CancelExited: P{e.PlayerId} {e.MoveId} cat={e.Category}");
        _dbgHit ??= e =>
        {
            GD.Print($"[DEBUG] HitConnected: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.HitAdvantage} dmg={e.Damage}");
            if (e.DefenderId == 2)
                _stateRecovery.Start(CharacterState.Hitstun);
        };
        _dbgBlock ??= e =>
        {
            GD.Print($"[DEBUG] MoveBlocked: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.BlockAdvantage} dmg={e.Damage}");
            if (e.DefenderId == 2)
                _stateRecovery.Start(CharacterState.Blockstun);
        };
        _dbgInput ??= e =>
            GD.Print($"[DEBUG] InputRecv: P{e.PlayerId} frame={e.Frame} type={e.InputType} val={e.InputValue}");
        _dbgReplayStart ??= e =>
            GD.Print($"[DEBUG] ReplayStarted: {e.TotalFrames} frames, dataVersion={e.DataVersion}");
        _dbgReplayEnd ??= e =>
            GD.Print($"[DEBUG] ReplayEnded: {e.TotalFramesPlayed} frames played");
        _trainingFrameAdvanced ??= _ =>
        {
            if (StateMachine is not null)
                _stateRecovery.AdvanceProcessedFrame(StateMachine);
        };

        EventBus.Instance.Subscribe(_dbgMoveFrame);
        EventBus.Instance.Subscribe(_dbgCancelEnter);
        EventBus.Instance.Subscribe(_dbgCancelExit);
        EventBus.Instance.Subscribe(_dbgHit);
        EventBus.Instance.Subscribe(_dbgBlock);
        EventBus.Instance.Subscribe(_dbgInput);
        EventBus.Instance.Subscribe(_dbgReplayStart!);
        EventBus.Instance.Subscribe(_dbgReplayEnd!);
        EventBus.Instance.Subscribe(_trainingFrameAdvanced);
    }

    private void UnsubscribeDebugEvents()
    {
        if (_dbgMoveFrame is not null) EventBus.Instance.Unsubscribe(_dbgMoveFrame);
        if (_dbgCancelEnter is not null) EventBus.Instance.Unsubscribe(_dbgCancelEnter);
        if (_dbgCancelExit is not null) EventBus.Instance.Unsubscribe(_dbgCancelExit);
        if (_dbgHit is not null) EventBus.Instance.Unsubscribe(_dbgHit);
        if (_dbgBlock is not null) EventBus.Instance.Unsubscribe(_dbgBlock);
        if (_dbgInput is not null) EventBus.Instance.Unsubscribe(_dbgInput);
        if (_dbgReplayStart is not null) EventBus.Instance.Unsubscribe(_dbgReplayStart);
        if (_dbgReplayEnd is not null) EventBus.Instance.Unsubscribe(_dbgReplayEnd);
        if (_trainingFrameAdvanced is not null) EventBus.Instance.Unsubscribe(_trainingFrameAdvanced);
    }
}

internal sealed class TrainingStateRecovery
{
    internal const int HitstunFrames = 30;
    internal const int BlockstunFrames = 20;
    private CharacterState? _expectedState;
    private int _remainingFrames;

    internal void Start(CharacterState state)
    {
        _expectedState = state;
        _remainingFrames = state == CharacterState.Hitstun ? HitstunFrames : BlockstunFrames;
    }

    internal void Cancel()
    {
        _expectedState = null;
        _remainingFrames = 0;
    }

    internal void AdvanceProcessedFrame(IStateMachine stateMachine)
    {
        if (_expectedState is not { } expected) return;
        if (stateMachine.GetCurrentState(2) != expected)
        {
            Cancel();
            return;
        }

        _remainingFrames--;
        if (_remainingFrames > 0) return;
        Cancel();
        stateMachine.PopState(2);
    }

    internal void RestoreIfOwned(IStateMachine stateMachine)
    {
        if (_expectedState is { } expected &&
            stateMachine.GetCurrentState(2) == expected)
        {
            stateMachine.PopState(2);
        }
        Cancel();
    }
}
