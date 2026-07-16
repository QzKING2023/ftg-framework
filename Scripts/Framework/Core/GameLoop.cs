#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Data;
using Godot;

namespace FTG_Framework.Core;

public partial class GameLoop : Node
{
    private IDataStore? _dataStore;
    private readonly List<IModule> _modules = new();

    public override void _Ready()
    {
        try
        {
            var moves = MoveDataLoader.LoadFromFile("res://Scripts/Framework/Data/example_moves.json");
            _dataStore = new DataStore(moves);
            GD.Print($"[Data] Loaded {moves.Length} moves.");
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