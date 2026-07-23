#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Input;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.Core;

public partial class GameLoop : Node
{
    private IDataStore? _dataStore;
    private IInputHistory? _inputHistory;
    private IInputLeniency? _leniencyMatcher;
    private IInputBuffer? _inputBuffer;
    private IChargeTracker? _chargeTracker;
    private IPriorityResolver? _priorityResolver;
    private IFrameDataEngine? _frameDataEngine;
    private readonly List<IModule> _modules = new();
    private bool _prevBtnA, _prevBtnB, _prevBtnC, _prevBtnD;
    private PlaybackControls? _playbackControls;
    private HitboxOverlay? _hitboxOverlay;
    private FrameDataPanel? _frameDataPanel;
    private AdvantageDisplay? _advantageDisplay;
    private InputLog? _inputLog;
    private IComboExecutor? _comboExecutor;

    // Test control state — edge-tracking prevents repeating triggers per press
    private bool _prevPauseKey, _prevStepFwdKey, _prevStepBackKey;
    private bool _prevHitKey, _prevBlockKey, _prevOverlayKey;
    private bool _prevShowP1Key, _prevShowP2Key;
    private bool _overlayEnabled;

    public override void _Ready()
    {
        try
        {
            FrameworkLog.Info = GD.Print;
            FrameworkLog.Error = GD.PrintErr;

            var moves = MoveDataLoader.LoadFromFile("res://Scripts/Framework/Data/example_moves.json");

            var gatlingPath = "res://Scripts/Framework/Data/example_gatling.json";
            GatlingTable[] gatlingTables;
            if (Godot.FileAccess.FileExists(gatlingPath))
            {
                gatlingTables = GatlingDataLoader.LoadFromFile(gatlingPath);
                GD.Print($"[Data] Loaded {gatlingTables.Length} gatling tables.");
            }
            else
            {
                gatlingTables = Array.Empty<GatlingTable>();
            }

            _dataStore = new DataStore(moves, gatlingTables);
            GD.Print($"[Data] Loaded {moves.Length} moves.");

            var inputHistory = new global::FTG_Framework.Input.InputHistory(capacity: 600);
            RegisterModule(inputHistory);
            _inputHistory = inputHistory;

            var chargeTracker = new global::FTG_Framework.Input.ChargeTracker(inputHistory);
            RegisterModule(chargeTracker);
            _chargeTracker = chargeTracker;

            var leniencyMatcher = new global::FTG_Framework.Input.InputLeniencyMatcher(inputHistory);

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "dp_c",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward }
                },
                RequiredButton = ButtonValue.C,
                Category = MoveCategory.Special
            });

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "dp_d",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward }
                },
                RequiredButton = ButtonValue.D,
                Category = MoveCategory.Special
            });

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "fireball_c",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward }
                },
                RequiredButton = ButtonValue.C,
                Category = MoveCategory.Special
            });

            RegisterModule(leniencyMatcher);
            _leniencyMatcher = leniencyMatcher;

            var inputBuffer = new global::FTG_Framework.Input.InputBuffer(inputHistory, leniencyMatcher, bufferDuration: 6, motionWindow: 30);
            RegisterModule(inputBuffer);
            _inputBuffer = inputBuffer;

            var priorityResolver = new global::FTG_Framework.Input.DefaultPriorityResolver(leniencyMatcher, chargeTracker);
            RegisterModule(priorityResolver);
            _priorityResolver = priorityResolver;

            var frameDataEngine = new FrameDataEngine(_dataStore);
            RegisterModule(frameDataEngine);
            _frameDataEngine = frameDataEngine;

            _comboExecutor = new ComboExecutor(_dataStore);

            // --- Training-mode UI panels ---

            _frameDataPanel = new FrameDataPanel
            {
                PanelPosition = new Vector2(10, 10),
                DataStore = _dataStore
            };
            AddChild(_frameDataPanel);

            _advantageDisplay = new AdvantageDisplay
            {
                PanelPosition = new Vector2(10, 75)
            };
            AddChild(_advantageDisplay);

            _inputLog = new InputLog
            {
                PanelPosition = new Vector2(10, 110),
                InputHistory = _inputHistory
            };
            AddChild(_inputLog);

            _playbackControls = new PlaybackControls
            {
                FrameDataEngine = _frameDataEngine,
                InputLog = _inputLog
            };
            AddChild(_playbackControls);

            _hitboxOverlay = new HitboxOverlay();
            AddChild(_hitboxOverlay);

            // --- Debug event logging — outputs to Godot console for verification ---
            SubscribeDebugEvents();
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex.Message);
            SetProcess(false);
            throw;
        }
    }

    public override void _Process(double delta)
    {
        if (_dataStore is null)
            return;

        bool processFrame = ShouldProcessFrame(EventBus.Instance.Paused, EventBus.Instance.StepRequested);
        EventBus.Instance.StepRequested = false;

        // Keys are polled even while paused so edge state (_prevBtn*) stays current —
        // a button held across the resume boundary must not produce a synthetic
        // rising edge. Inputs are only recorded when a frame is processed.
        bool back = Godot.Input.IsKeyPressed(Key.A);
        bool forward = Godot.Input.IsKeyPressed(Key.D);
        bool down = Godot.Input.IsKeyPressed(Key.S);
        bool up = Godot.Input.IsKeyPressed(Key.Space);
        bool btnA = Godot.Input.IsKeyPressed(Key.U);
        bool btnB = Godot.Input.IsKeyPressed(Key.I);
        bool btnC = Godot.Input.IsKeyPressed(Key.K);
        bool btnD = Godot.Input.IsKeyPressed(Key.J);

        // Test-key polling — always active (even while paused).
        bool pauseKey = Godot.Input.IsKeyPressed(Key.P);
        bool stepFwd = Godot.Input.IsKeyPressed(Key.Right);
        bool stepBack = Godot.Input.IsKeyPressed(Key.Left);
        bool hitKey = Godot.Input.IsKeyPressed(Key.H);
        bool blockKey = Godot.Input.IsKeyPressed(Key.B);
        bool overlayKey = Godot.Input.IsKeyPressed(Key.O);
        bool showP1Key = Godot.Input.IsKeyPressed(Key.Key1);
        bool showP2Key = Godot.Input.IsKeyPressed(Key.Key2);

        ProcessTestKeys(pauseKey, stepFwd, stepBack, hitKey, blockKey, overlayKey, showP1Key, showP2Key);

        if (!processFrame)
        {
            _prevBtnA = btnA;
            _prevBtnB = btnB;
            _prevBtnC = btnC;
            _prevBtnD = btnD;
            return;
        }

        // Direction auto-combine
        var dir = ComputeDirection(back, forward, down, up);
        _inputHistory?.RecordInput(1, InputType.Directional, (int)dir);

        // Button inputs — record once on the rising edge of each press
        if (btnA && !_prevBtnA) _inputHistory?.RecordInput(1, InputType.Button, (int)ButtonValue.A);
        if (btnB && !_prevBtnB) _inputHistory?.RecordInput(1, InputType.Button, (int)ButtonValue.B);
        if (btnC && !_prevBtnC) _inputHistory?.RecordInput(1, InputType.Button, (int)ButtonValue.C);
        if (btnD && !_prevBtnD) _inputHistory?.RecordInput(1, InputType.Button, (int)ButtonValue.D);
        _prevBtnA = btnA;
        _prevBtnB = btnB;
        _prevBtnC = btnC;
        _prevBtnD = btnD;

        _chargeTracker?.Update(1, EventBus.Instance.CurrentFrame);

        var matches = _inputBuffer?.TryMatch(1);
        var resolved = _priorityResolver?.Resolve(matches ?? Array.Empty<MatchResult>(), 1, EventBus.Instance.CurrentFrame);
        if (resolved != null && _frameDataEngine is not null && _frameDataEngine.GetPhase(1) == MovePhase.Idle)
            _frameDataEngine.StartMove(1, resolved.Value.MoveId);

        _frameDataEngine?.Update();

        EventBus.Instance.ProcessFrame();
    }

    public void RegisterModule(IModule module)
    {
        if (_dataStore is null)
            throw new InvalidOperationException("[Core] Cannot register module before DataStore is initialized.");
        _modules.Add(module);
        module.Initialize(_dataStore);
    }

    internal static bool ShouldProcessFrame(bool paused, bool stepRequested) => !paused || stepRequested;

    internal static DirectionValue ComputeDirection(bool back, bool forward, bool down, bool up)
    {
        int v = (up ? 1 : 0) + (down ? 2 : 0);
        int h = (back ? 1 : 0) + (forward ? 2 : 0);
        if (v > 2 || h > 2) return DirectionValue.Neutral;
        return s_dirLookup[v, h];
    }

    private static readonly DirectionValue[,] s_dirLookup = new DirectionValue[3, 3]
    {
        { DirectionValue.Neutral, DirectionValue.Back, DirectionValue.Forward },
        { DirectionValue.Up, DirectionValue.UpBack, DirectionValue.UpForward },
        { DirectionValue.Down, DirectionValue.DownBack, DirectionValue.DownForward }
    };

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
            _frameDataEngine?.RegisterHit(attackerId: 1, defenderId: 2, moveId: "5LP", isBlocked: false);

        if (blockKey && !_prevBlockKey)
            _frameDataEngine?.RegisterHit(attackerId: 1, defenderId: 2, moveId: "5HP", isBlocked: true);

        if (overlayKey && !_prevOverlayKey)
        {
            _overlayEnabled = !_overlayEnabled;
            _hitboxOverlay!.Enabled = _overlayEnabled;
            GD.Print($"[Test] HitboxOverlay Enabled = {_overlayEnabled}");
        }

        if (showP1Key && !_prevShowP1Key && _inputLog != null)
        {
            _inputLog.ShowP1 = !_inputLog.ShowP1;
            GD.Print($"[Test] InputLog ShowP1 = {_inputLog.ShowP1}");
        }

        if (showP2Key && !_prevShowP2Key && _inputLog != null)
        {
            _inputLog.ShowP2 = !_inputLog.ShowP2;
            GD.Print($"[Test] InputLog ShowP2 = {_inputLog.ShowP2}");
        }

        _prevPauseKey = pauseKey;
        _prevStepFwdKey = stepFwd;
        _prevStepBackKey = stepBack;
        _prevHitKey = hitKey;
        _prevBlockKey = blockKey;
        _prevOverlayKey = overlayKey;
        _prevShowP1Key = showP1Key;
        _prevShowP2Key = showP2Key;
    }

    private void SubscribeDebugEvents()
    {
        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(e =>
            GD.Print($"[DEBUG] MoveFrame: P{e.PlayerId} {e.MoveId} phase={e.Phase} frame={e.CurrentFrame}/{e.TotalFrames}"));

        EventBus.Instance.Subscribe<CancelWindowEnteredEvent>(e =>
            GD.Print($"[DEBUG] CancelEnter: P{e.PlayerId} {e.MoveId} cat={e.Category} [{e.StartFrame}-{e.EndFrame}]"));

        EventBus.Instance.Subscribe<CancelWindowExitedEvent>(e =>
            GD.Print($"[DEBUG] CancelExited: P{e.PlayerId} {e.MoveId} cat={e.Category}"));

        EventBus.Instance.Subscribe<HitConnectedEvent>(e =>
            GD.Print($"[DEBUG] HitConnected: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.HitAdvantage} dmg={e.Damage}"));

        EventBus.Instance.Subscribe<MoveBlockedEvent>(e =>
            GD.Print($"[DEBUG] MoveBlocked: {e.AttackerId}->{e.DefenderId} {e.MoveId} adv={e.BlockAdvantage} dmg={e.Damage}"));

        EventBus.Instance.Subscribe<InputReceivedEvent>(e =>
            GD.Print($"[DEBUG] InputRecv: P{e.PlayerId} frame={e.Frame} type={e.InputType} val={e.InputValue}"));
    }
}
