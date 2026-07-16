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
    private readonly List<IModule> _modules = new();

    public override void _Ready()
    {
        try
        {
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
                MoveId = "dp_p",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Forward, DirectionValue.Down, DirectionValue.DownForward },
                    new[] { DirectionValue.Forward, DirectionValue.DownForward, DirectionValue.Forward },
                    new[] { DirectionValue.DownForward, DirectionValue.Down, DirectionValue.DownForward }
                },
                RequiredButton = ButtonValue.HP,
                Category = MoveCategory.Special
            });

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "fireball_p",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward }
                },
                RequiredButton = ButtonValue.HP,
                Category = MoveCategory.Special
            });

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "sonic_boom",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Forward }
                },
                RequiredButton = ButtonValue.HP,
                ChargeDirection = DirectionValue.Back,
                MinChargeDuration = 30,
                Category = MoveCategory.Special
            });

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "super_fireball",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward,
                             DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward }
                },
                RequiredButton = ButtonValue.HP,
                Category = MoveCategory.Super
            });

            RegisterModule(leniencyMatcher);
            _leniencyMatcher = leniencyMatcher;

            var inputBuffer = new global::FTG_Framework.Input.InputBuffer(inputHistory, leniencyMatcher);
            RegisterModule(inputBuffer);
            _inputBuffer = inputBuffer;

            var priorityResolver = new global::FTG_Framework.Input.DefaultPriorityResolver(leniencyMatcher, chargeTracker);
            RegisterModule(priorityResolver);
            _priorityResolver = priorityResolver;
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

        if (Godot.Input.IsKeyPressed(Key.P))
            _inputHistory?.RecordInput(1, InputType.Button, (int)ButtonValue.HP);
        if (Godot.Input.IsKeyPressed(Key.D))
            _inputHistory?.RecordInput(1, InputType.Directional, (int)DirectionValue.Forward);
        if (Godot.Input.IsKeyPressed(Key.S))
            _inputHistory?.RecordInput(1, InputType.Directional, (int)DirectionValue.Down);
        if (Godot.Input.IsKeyPressed(Key.C))
            _inputHistory?.RecordInput(1, InputType.Directional, (int)DirectionValue.DownForward);
        if (Godot.Input.IsKeyPressed(Key.A))
            _inputHistory?.RecordInput(1, InputType.Directional, (int)DirectionValue.Back);

        _chargeTracker?.Update(1, EventBus.Instance.CurrentFrame);

        var matches = _inputBuffer?.TryMatch(1);
        var resolved = _priorityResolver?.Resolve(matches ?? Array.Empty<MatchResult>(), 1, EventBus.Instance.CurrentFrame);
        if (resolved != null)
        {
#if DEBUG
            GD.Print($"[Input] Move detected: {resolved.Value.MoveId} (button: {resolved.Value.RequiredButton}) at frame {resolved.Value.MatchedAtFrame}");
#endif
        }

        EventBus.Instance.ProcessFrame();
    }

    public void RegisterModule(IModule module)
    {
        if (_dataStore is null)
            throw new InvalidOperationException("[Core] Cannot register module before DataStore is initialized.");
        _modules.Add(module);
        module.Initialize(_dataStore);
    }
}
