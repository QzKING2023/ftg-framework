#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core.Events;
using FTG_Framework.Core.Replay;
using FTG_Framework.Data;
using FTG_Framework.Engine.Combo;
using FTG_Framework.Engine.FrameData;
using FTG_Framework.Input;
using FTG_Framework.Scenes;
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
    private IComboExecutor? _comboExecutor;
    private ISOCDResolver? _socdResolver;
    private ISceneManager? _sceneManager;
    private ReplayOrchestrator? _replayOrchestrator;

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

            var charactersPath = "res://Scripts/Framework/Data/example_characters.json";
            if (Godot.FileAccess.FileExists(charactersPath))
            {
                var characters = CharacterDataLoader.LoadFromFile(charactersPath);
                foreach (var c in characters)
                    _dataStore.RegisterCharacter(c);
                GD.Print($"[Data] Loaded {characters.Length} characters.");
            }
            else
            {
                FrameworkLog.Info?.Invoke("[Data] No character roster file found — character select will be inert.");
            }

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

            var comboExecutor = new ComboExecutor(_dataStore, frameDataEngine);
            _comboExecutor = comboExecutor;
            RegisterModule(comboExecutor);

            var comboStateTracker = new ComboStateTracker(_dataStore);
            RegisterModule(comboStateTracker);

            _socdResolver = new global::FTG_Framework.Input.DefaultSOCDResolver();

            _replayOrchestrator = new ReplayOrchestrator(_frameDataEngine);

            // ── Scene setup ──
            _sceneManager = new SceneManager(this);

            var characterSelectScene = new CharacterSelectScene
            {
                DataStore = _dataStore
            };

            var trainingScene = new TrainingScene
            {
                DataStore = _dataStore,
                InputHistory = _inputHistory,
                FrameDataEngine = _frameDataEngine
            };

            _sceneManager.RegisterScene("character_select", () => new CharacterSelectScene
            {
                DataStore = _dataStore
            });

            _sceneManager.RegisterScene("training", () => new TrainingScene
            {
                DataStore = _dataStore,
                InputHistory = _inputHistory,
                FrameDataEngine = _frameDataEngine
            });

            _sceneManager.GoTo("character_select");
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

        bool back = Godot.Input.IsKeyPressed(Key.A);
        bool forward = Godot.Input.IsKeyPressed(Key.D);
        bool down = Godot.Input.IsKeyPressed(Key.S);
        bool up = Godot.Input.IsKeyPressed(Key.Space);
        bool btnA = Godot.Input.IsKeyPressed(Key.U);
        bool btnB = Godot.Input.IsKeyPressed(Key.I);
        bool btnC = Godot.Input.IsKeyPressed(Key.K);
        bool btnD = Godot.Input.IsKeyPressed(Key.J);

        if (!processFrame)
        {
            _prevBtnA = btnA;
            _prevBtnB = btnB;
            _prevBtnC = btnC;
            _prevBtnD = btnD;
            return;
        }

        // Replay mode — inject recorded events, skip live input
        if (_replayOrchestrator is { IsPlaying: true })
        {
            if (!_replayOrchestrator.ProcessReplayFrame())
                return;
            _frameDataEngine?.Update();
            EventBus.Instance.ProcessFrame();
            _prevBtnA = btnA;
            _prevBtnB = btnB;
            _prevBtnC = btnC;
            _prevBtnD = btnD;
            return;
        }

        // SOCD cleaning + direction combine
        var dir = _socdResolver is not null
            ? SafeResolve(_socdResolver, back, forward, down, up)
            : FallbackDirection(back, forward, down, up);
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
        if (resolved != null && _frameDataEngine is not null)
        {
            if (_frameDataEngine.GetPhase(1) == MovePhase.Idle)
            {
                _frameDataEngine.StartMove(1, resolved.Value.MoveId);
            }
            else if (_comboExecutor?.TryCancel(1, resolved.Value.MoveId) == true)
            {
                // Cancel executed — MoveCanceled published, FrameDataEngine handles interrupt
            }
        }

        _frameDataEngine?.Update();

        EventBus.Instance.ProcessFrame();

        // Capture FrameDataEngine snapshot for replay recording.
        _replayOrchestrator?.CaptureSnapshot(EventBus.Instance.CurrentFrame - 1);
    }

    public void RegisterModule(IModule module)
    {
        if (_dataStore is null)
            throw new InvalidOperationException("[Core] Cannot register module before DataStore is initialized.");
        _modules.Add(module);
        module.Initialize(_dataStore);
    }

    public override void _ExitTree()
    {
        ShutdownModules(_modules);
    }

    // Reverse order: dependents (combo modules) detach before the engines whose
    // events they consume. Clearing prevents a double shutdown if _ExitTree re-fires.
    internal static void ShutdownModules(List<IModule> modules)
    {
        for (int i = modules.Count - 1; i >= 0; i--)
            modules[i].Shutdown();
        modules.Clear();
    }

    internal static bool ShouldProcessFrame(bool paused, bool stepRequested) => !paused || stepRequested;

    internal static DirectionValue ComputeDirection(bool back, bool forward, bool down, bool up)
    {
        int v = (up ? 1 : 0) + (down ? 2 : 0);
        int h = (back ? 1 : 0) + (forward ? 2 : 0);
        if (v > 2 || h > 2) return DirectionValue.Neutral;
        return s_dirLookup[v, h];
    }

    internal static DirectionValue SafeResolve(ISOCDResolver resolver, bool left, bool right, bool down, bool up)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var result = resolver.Resolve(left, right, down, up);
        if (!Enum.IsDefined(typeof(DirectionValue), result))
        {
            FrameworkLog.Error($"[Input] SOCD resolver returned invalid direction value {(int)result} — falling back to Neutral");
            return DirectionValue.Neutral;
        }
        return result;
    }

    private static DirectionValue FallbackDirection(bool left, bool right, bool down, bool up)
    {
        FrameworkLog.Error("[Input] SOCD resolver is null — falling back to direct ComputeDirection");
        return ComputeDirection(left, right, down, up);
    }

    private static readonly DirectionValue[,] s_dirLookup = new DirectionValue[3, 3]
    {
        { DirectionValue.Neutral, DirectionValue.Back, DirectionValue.Forward },
        { DirectionValue.Up, DirectionValue.UpBack, DirectionValue.UpForward },
        { DirectionValue.Down, DirectionValue.DownBack, DirectionValue.DownForward }
    };

    private bool _prevBtnA, _prevBtnB, _prevBtnC, _prevBtnD;
}
