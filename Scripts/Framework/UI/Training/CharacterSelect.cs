#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public partial class CharacterSelect : Control
{
    [Export] public Vector2 PanelPosition { get; set; } = new(200, 200);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 18;

    public IDataStore? DataStore { get; set; }

    private CharacterSelectViewModel? _vm;
    private Label? _p1Label;
    private Label? _p2Label;
    private Label? _statusLabel;

    internal CharacterSelectViewModel? ViewModel => _vm;

    public void AutoConfirmBoth()
    {
        if (_vm is null) return;
        if (_vm.Roster.Count == 0) return;
        _vm.ConfirmP1();
        _vm.ConfirmP2();
        _RefreshDisplay();
    }

    public override void _Ready()
    {
        if (DataStore is null)
        {
            GD.PushError("[CharacterSelect] DataStore is null — control will be inert.");
            return;
        }

        _vm = new CharacterSelectViewModel(DataStore);

        _p1Label = new Label { Position = new Vector2(0, 0) };
        _p1Label.AddThemeColorOverride("font_color", TextColor);
        _p1Label.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_p1Label);

        _p2Label = new Label { Position = new Vector2(300, 0) };
        _p2Label.AddThemeColorOverride("font_color", TextColor);
        _p2Label.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_p2Label);

        _statusLabel = new Label { Position = new Vector2(0, 40) };
        _statusLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        _statusLabel.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_statusLabel);

        Position = PanelPosition;

        EventBus.Instance.Subscribe<CharacterSelectedEvent>(_OnCharacterSelected);
        EventBus.Instance.Subscribe<MatchInitializedEvent>(_OnMatchInitialized);

        _RefreshDisplay();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.Unsubscribe<CharacterSelectedEvent>(_OnCharacterSelected);
        EventBus.Instance.Unsubscribe<MatchInitializedEvent>(_OnMatchInitialized);
    }

    public override void _Process(double delta)
    {
        if (_vm is null) return;

        // P1 controls: A/D browse, W confirm, S cancel
        if (Godot.Input.IsKeyPressed(Key.D) && !_prevP1BrowseRight)
        {
            _vm.BrowseP1Next();
            _RefreshDisplay();
        }
        if (Godot.Input.IsKeyPressed(Key.A) && !_prevP1BrowseLeft)
        {
            _vm.BrowseP1Previous();
            _RefreshDisplay();
        }
        if (Godot.Input.IsKeyPressed(Key.W) && !_prevP1Confirm)
        {
            _vm.ConfirmP1();
            _RefreshDisplay();
        }
        if (Godot.Input.IsKeyPressed(Key.S) && !_prevP1Cancel)
        {
            _vm.CancelP1();
            _RefreshDisplay();
        }

        // P2 controls: Right/Left browse, Up confirm, Down cancel
        if (Godot.Input.IsKeyPressed(Key.Right) && !_prevP2BrowseRight)
        {
            _vm.BrowseP2Next();
            _RefreshDisplay();
        }
        if (Godot.Input.IsKeyPressed(Key.Left) && !_prevP2BrowseLeft)
        {
            _vm.BrowseP2Previous();
            _RefreshDisplay();
        }
        if (Godot.Input.IsKeyPressed(Key.Up) && !_prevP2Confirm)
        {
            _vm.ConfirmP2();
            _RefreshDisplay();
        }
        if (Godot.Input.IsKeyPressed(Key.Down) && !_prevP2Cancel)
        {
            _vm.CancelP2();
            _RefreshDisplay();
        }

        _prevP1BrowseRight = Godot.Input.IsKeyPressed(Key.D);
        _prevP1BrowseLeft = Godot.Input.IsKeyPressed(Key.A);
        _prevP1Confirm = Godot.Input.IsKeyPressed(Key.W);
        _prevP1Cancel = Godot.Input.IsKeyPressed(Key.S);

        _prevP2BrowseRight = Godot.Input.IsKeyPressed(Key.Right);
        _prevP2BrowseLeft = Godot.Input.IsKeyPressed(Key.Left);
        _prevP2Confirm = Godot.Input.IsKeyPressed(Key.Up);
        _prevP2Cancel = Godot.Input.IsKeyPressed(Key.Down);
    }

    private void _RefreshDisplay()
    {
        if (_vm is null) return;

        var p1Name = _vm.P1CharacterName ?? "—";
        var p2Name = _vm.P2CharacterName ?? "—";
        var p1Marker = _vm.P1Locked ? " ✓" : "";
        var p2Marker = _vm.P2Locked ? " ✓" : "";

        if (_p1Label is not null)
            _p1Label.Text = $"P1: <{p1Name}>{p1Marker}";
        if (_p2Label is not null)
            _p2Label.Text = $"P2: <{p2Name}>{p2Marker}";
        if (_statusLabel is not null)
            _statusLabel.Text = _vm.BothReady ? "BOTH READY — Match starting!" : "Select your character";
    }

    private void _OnCharacterSelected(CharacterSelectedEvent e)
    {
        GD.Print($"[CharacterSelect] P{e.PlayerSlot} selected {e.CharacterId}");
    }

    private void _OnMatchInitialized(MatchInitializedEvent e)
    {
        GD.Print($"[CharacterSelect] Match initialized: {e.P1CharacterId} vs {e.P2CharacterId}");
    }

    // Edge-tracking fields
    private bool _prevP1BrowseRight, _prevP1BrowseLeft, _prevP1Confirm, _prevP1Cancel;
    private bool _prevP2BrowseRight, _prevP2BrowseLeft, _prevP2Confirm, _prevP2Cancel;
}
