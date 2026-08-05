#nullable enable
using System;
using FTG_Framework.Characters;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Input;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.Scenes;

public partial class TrainingScene : Node, IScene
{
    public IDataStore? DataStore { get; set; }
    public IInputHistory? InputHistory { get; set; }
    public IFrameDataEngine? FrameDataEngine { get; set; }
    public IStateMachine? StateMachine { get; set; }
    public IRuntimeTuningService? RuntimeTuningService { get; set; }
    public RuntimeTuningSessionAuthority? RuntimeTuningSessions { get; set; }
    public TrainingInputService? TrainingInputService { get; set; }
    public TrainingInputRecordingLibrary? TrainingRecordingLibrary { get; set; }
    public string P1CharacterId { get; set; } = string.Empty;
    public string P2CharacterId { get; set; } = string.Empty;

    private PlaybackControls? _playbackControls;
    private HitboxOverlay? _hitboxOverlay;
    private InputLog? _inputLog;
    private bool _overlayEnabled;
    private RuntimeTuningPanel? _runtimeTuningPanel;
    private ComboDisplay? _comboDisplay;
    private CharacterController? _p1Character;
    private CharacterController? _p2Character;
    private DiagnosticsTestHarness? _diagnostics;
    private Label? _diagnosticsLabel;
    private TrainingInputPlaybackPanel? _trainingInputPanel;
    private TrainingPresentationAdapter? _presentationAdapter;
    private Node2D? _worldPresentationRoot;
    private CanvasLayer? _trainingUiLayer;

    public void Enter(ISceneManager manager)
    {
        _worldPresentationRoot = new Node2D { Name = "WorldPresentationRoot" };
        AddChild(_worldPresentationRoot);
        var camera = new Camera2D
        {
            Name = "TrainingPresentationCamera",
            Enabled = true,
            Position = new Vector2(576, 324)
        };
        _worldPresentationRoot.AddChild(camera);

        InstantiateCharacters(_worldPresentationRoot);

        var presentation = new TestMatchPresentation();
        if (_p1Character is not null && _p2Character is not null)
            presentation.Bind(_p1Character, _p2Character);
        _worldPresentationRoot.AddChild(presentation);

        _trainingUiLayer = new CanvasLayer { Name = "TrainingUiLayer" };
        AddChild(_trainingUiLayer);
        double uiScale = ProjectSettings.GetSetting("ftg/training/ui_scale", 1.0).AsDouble();
        var trainingUiRoot = new Control
        {
            Name = "TrainingUiRoot",
            Theme = new Theme { DefaultBaseScale = (float)uiScale }
        };
        trainingUiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _trainingUiLayer.AddChild(trainingUiRoot);
        var leftRegion = CreateRegion("LeftDiagnosticsRegion", trainingUiRoot);
        var leftDrawer = CreateRegion("LeftDiagnosticsDrawer", trainingUiRoot);
        var topRightRegion = CreateRegion("TopRightTuningRegion", trainingUiRoot);
        var bottomRightRegion = CreateRegion("BottomRightPlaybackRegion", trainingUiRoot);
        var diagnosticsToggle = new Button
        {
            Name = "OpenDiagnosticsDrawer",
            Text = "Diagnostics",
            FocusMode = Control.FocusModeEnum.All
        };
        diagnosticsToggle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        leftRegion.AddChild(diagnosticsToggle);
        var diagnosticsClose = new Button
        {
            Name = "CloseDiagnosticsDrawer",
            Text = "Close diagnostics",
            FocusMode = Control.FocusModeEnum.All
        };
        diagnosticsClose.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        diagnosticsClose.OffsetBottom = 36;
        leftDrawer.AddChild(diagnosticsClose);
        var leftScroll = new ScrollContainer
        {
            Name = "LeftDiagnosticsScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        leftScroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        leftScroll.OffsetTop = 40;
        leftScroll.OffsetBottom = -148;
        leftDrawer.AddChild(leftScroll);
        var leftContent = new VBoxContainer
        {
            Name = "LeftDiagnosticsContent",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        leftScroll.AddChild(leftContent);
        var gameplayLegend = new ControlsLegend { Name = "GameplayLegend" };
        gameplayLegend.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        gameplayLegend.OffsetTop = -140;
        gameplayLegend.OffsetBottom = 0;
        leftDrawer.AddChild(gameplayLegend);

        bool settingEnabled = ProjectSettings.GetSetting(
            "ftg/test_harness/enabled", false).AsBool();
        _diagnostics = new DiagnosticsTestHarness(
            new DiagnosticsPolicy(settingEnabled, OS.IsDebugBuild()));
        _diagnosticsLabel = new Label
        {
            Text = settingEnabled && OS.IsDebugBuild()
                ? DiagnosticsTestHarness.TestModeText
                : "DIAGNOSTICS DISABLED — gameplay evidence only",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        leftContent.AddChild(_diagnosticsLabel);

        var frameDataPanel = new FrameDataPanel
        {
            DataStore = DataStore
        };
        leftContent.AddChild(frameDataPanel);

        var advantageDisplay = new AdvantageDisplay();
        leftContent.AddChild(advantageDisplay);

        _comboDisplay = new ComboDisplay();
        leftContent.AddChild(_comboDisplay);

        _inputLog = new InputLog
        {
            InputHistory = InputHistory
        };
        leftContent.AddChild(_inputLog);

        _playbackControls = new PlaybackControls
        {
            FrameDataEngine = FrameDataEngine,
            InputLog = _inputLog
        };
        leftContent.AddChild(_playbackControls);

        if (TrainingInputService is not null && TrainingRecordingLibrary is not null)
        {
            _trainingInputPanel = new TrainingInputPlaybackPanel
            {
                Service = TrainingInputService,
                Library = TrainingRecordingLibrary
            };
            bottomRightRegion.AddChild(_trainingInputPanel);
            AddChild(new TrainingShortcutRouter
            {
                Name = "TrainingShortcutRouter",
                Panel = _trainingInputPanel
            });
        }

        _hitboxOverlay = new HitboxOverlay();
        _worldPresentationRoot.AddChild(_hitboxOverlay);

        var debugPanel = new EventBusDebugPanel();
        leftContent.AddChild(debugPanel);

        if (RuntimeTuningService is not null && RuntimeTuningSessions is not null)
        {
            _runtimeTuningPanel = new RuntimeTuningPanel
            {
                Service = RuntimeTuningService,
                DataStore = DataStore,
                Sessions = RuntimeTuningSessions
            };
            topRightRegion.AddChild(_runtimeTuningPanel);
        }

        _presentationAdapter = new TrainingPresentationAdapter
        {
            Name = "TrainingPresentationAdapter",
            WorldCamera = camera,
            WorldPresentation = presentation,
            TrainingUiRoot = trainingUiRoot,
            LeftDiagnosticsRegion = leftRegion,
            LeftDiagnosticsDrawer = leftDrawer,
            DiagnosticsToggle = diagnosticsToggle,
            DiagnosticsClose = diagnosticsClose,
            TopRightTuningRegion = topRightRegion,
            BottomRightPlaybackRegion = bottomRightRegion,
            UiScale = uiScale
        };
        AddChild(_presentationAdapter);

        SubscribeDebugEvents();
    }

    private static Control CreateRegion(string name, Control parent)
    {
        var region = new Control { Name = name, ClipContents = true };
        parent.AddChild(region);
        return region;
    }

    private void InstantiateCharacters(Node2D worldRoot)
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

        var visibleRect = new Rect2(Vector2.Zero, new Vector2(
            (float)TrainingPresentationLayout.DesignWidth,
            (float)TrainingPresentationLayout.DesignHeight));
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
        _p1Character = p1;

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
        _p2Character = p2;

        worldRoot.AddChild(p1);
        worldRoot.AddChild(p2);
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
        _trainingInputPanel?.Shutdown();
        _trainingInputPanel = null;
        TrainingInputService?.CancelForLifecycle();
        _comboDisplay?.Shutdown();
        _comboDisplay = null;
        _runtimeTuningPanel?.Shutdown();
        _runtimeTuningPanel = null;
        _presentationAdapter = null;
        _trainingUiLayer = null;
        _worldPresentationRoot = null;
        if (StateMachine is not null)
            _diagnostics?.Reset(StateMachine);
        _diagnostics = null;
        _diagnosticsLabel = null;
        UnsubscribeDebugEvents();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        Control? focusOwner = GetViewport().GuiGetFocusOwner();
        if (@event is not InputEventMouseButton mouseButton ||
            !ShouldReleaseGuiFocus(
                mouseButton.ButtonIndex, mouseButton.Pressed, focusOwner is not null)) return;
        focusOwner!.ReleaseFocus();
    }

    internal static bool ShouldReleaseGuiFocus(
        MouseButton button, bool pressed, bool hasFocusOwner) =>
        hasFocusOwner && button == MouseButton.Left && pressed;

    public override void _Process(double delta)
    {
        bool pauseKey = Godot.Input.IsKeyPressed(Key.P);
        bool stepFwd = Godot.Input.IsKeyPressed(Key.Bracketright);
        bool stepBack = Godot.Input.IsKeyPressed(Key.Bracketleft);
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
            RunDiagnosticInjection(CharacterState.Hitstun);
        if (blockKey && !_prevBlockKey)
            RunDiagnosticInjection(CharacterState.Blockstun);

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

    private void RunDiagnosticInjection(CharacterState state)
    {
        if (_diagnostics is null || StateMachine is null) return;
        var result = _diagnostics.TryInjectP2(StateMachine, state);
        if (_diagnosticsLabel is not null) _diagnosticsLabel.Text = result.Message;
        if (result.Accepted) GD.Print(result.Message); else GD.PrintErr(result.Message);
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
        {
            GD.Print($"[DEBUG] HitConnected: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.HitAdvantage} dmg={e.Damage}");
        };
        _dbgBlock ??= e =>
        {
            GD.Print($"[DEBUG] MoveBlocked: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.BlockAdvantage} dmg={e.Damage}");
        };
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
