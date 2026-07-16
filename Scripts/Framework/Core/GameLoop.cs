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
                RequiredButton = ButtonValue.HP
            });

            leniencyMatcher.RegisterMove(new MoveInputConfig
            {
                MoveId = "fireball_p",
                AcceptedSequences = new DirectionValue[][]
                {
                    new[] { DirectionValue.Down, DirectionValue.DownForward, DirectionValue.Forward }
                },
                RequiredButton = ButtonValue.HP
            });

            RegisterModule(leniencyMatcher);
            _leniencyMatcher = leniencyMatcher;

            var inputBuffer = new global::FTG_Framework.Input.InputBuffer(inputHistory, leniencyMatcher);
            RegisterModule(inputBuffer);
            _inputBuffer = inputBuffer;
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

        var matches = _inputBuffer?.TryMatch(1);
        if (matches is { Count: > 0 })
        {
#if DEBUG
            foreach (var match in matches)
                GD.Print($"[Input] Move detected: {match.MoveId} (button: {match.RequiredButton}) at frame {match.MatchedAtFrame}");
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
