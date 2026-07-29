#nullable enable
using System;
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

    private PlaybackControls? _playbackControls;
    private HitboxOverlay? _hitboxOverlay;
    private InputLog? _inputLog;
    private bool _overlayEnabled;

    public void Enter(ISceneManager manager)
    {
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

    public void Exit()
    {
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

        if (hitKey && !_prevHitKey)
            FrameDataEngine?.RegisterHit(attackerId: 1, defenderId: 2, moveId: "5LP", isBlocked: false);

        if (blockKey && !_prevBlockKey)
            FrameDataEngine?.RegisterHit(attackerId: 1, defenderId: 2, moveId: "5HP", isBlocked: true);

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

    private void SubscribeDebugEvents()
    {
        _dbgMoveFrame ??= e =>
            GD.Print($"[DEBUG] MoveFrame: P{e.PlayerId} {e.MoveId} phase={e.Phase} frame={e.CurrentFrame}/{e.TotalFrames}");
        _dbgCancelEnter ??= e =>
            GD.Print($"[DEBUG] CancelEnter: P{e.PlayerId} {e.MoveId} cat={e.Category} [{e.StartFrame}-{e.EndFrame}]");
        _dbgCancelExit ??= e =>
            GD.Print($"[DEBUG] CancelExited: P{e.PlayerId} {e.MoveId} cat={e.Category}");
        _dbgHit ??= e =>
            GD.Print($"[DEBUG] HitConnected: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.HitAdvantage} dmg={e.Damage}");
        _dbgBlock ??= e =>
            GD.Print($"[DEBUG] MoveBlocked: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.BlockAdvantage} dmg={e.Damage}");
        _dbgInput ??= e =>
            GD.Print($"[DEBUG] InputRecv: P{e.PlayerId} frame={e.Frame} type={e.InputType} val={e.InputValue}");
        _dbgReplayStart ??= e =>
            GD.Print($"[DEBUG] ReplayStarted: {e.TotalFrames} frames, dataVersion={e.DataVersion}");
        _dbgReplayEnd ??= e =>
            GD.Print($"[DEBUG] ReplayEnded: {e.TotalFramesPlayed} frames played");

        EventBus.Instance.Subscribe(_dbgMoveFrame);
        EventBus.Instance.Subscribe(_dbgCancelEnter);
        EventBus.Instance.Subscribe(_dbgCancelExit);
        EventBus.Instance.Subscribe(_dbgHit);
        EventBus.Instance.Subscribe(_dbgBlock);
        EventBus.Instance.Subscribe(_dbgInput);
        EventBus.Instance.Subscribe(_dbgReplayStart!);
        EventBus.Instance.Subscribe(_dbgReplayEnd!);
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
    }
}
