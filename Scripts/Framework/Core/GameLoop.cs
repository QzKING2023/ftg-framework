#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Data;
using FTG_Framework.Input;
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
    private global::FTG_Framework.UI.Training.PlaybackControls? _playbackControls;
    private global::FTG_Framework.UI.Training.HitboxOverlay? _hitboxOverlay;

    public override void _Ready()
    {
        try
        {
            FrameworkLog.Info = GD.Print;
            FrameworkLog.Error = GD.PrintErr;

            var moves = MoveDataLoader.LoadFromFile("res://Scripts/Framework/Data/example_moves.json");
            _dataStore = new DataStore(moves);
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

            var frameDataEngine = new global::FTG_Framework.Engine.FrameData.FrameDataEngine(_dataStore);
            RegisterModule(frameDataEngine);
            _frameDataEngine = frameDataEngine;

            _playbackControls = new global::FTG_Framework.UI.Training.PlaybackControls
            {
                FrameDataEngine = _frameDataEngine
            };
            AddChild(_playbackControls);

            _hitboxOverlay = new global::FTG_Framework.UI.Training.HitboxOverlay();
            AddChild(_hitboxOverlay);
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
}
