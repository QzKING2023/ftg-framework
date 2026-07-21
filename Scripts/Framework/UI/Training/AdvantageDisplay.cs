#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.UI.Training;

public partial class AdvantageDisplay : Control
{
    [Export] public int TrackedPlayer { get; set; } = 1;
    [Export] public bool ShowWhenZero { get; set; } = true;
    [Export] public Vector2 PanelPosition { get; set; } = new(10, 75);
    [Export] public Vector2 PanelSize { get; set; } = new(120, 30);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 14;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    private int _advantage;
    private Label? _label;
    private ColorRect? _background;

    internal int Advantage => _advantage;
    internal string DisplayText => _label?.Text ?? string.Empty;
    internal bool IsLabelVisible => _label?.Visible ?? false;
    internal bool IsBackgroundVisible => _background?.Visible ?? false;

    public override void _Ready()
    {
        if (_label != null)
            return;

        if (TrackedPlayer < 1 || TrackedPlayer > 2)
            GD.PushError($"[AdvantageDisplay] TrackedPlayer must be 1 or 2, got {TrackedPlayer}. Display will remain inert.");

        _background = new ColorRect
        {
            Color = BackgroundColor,
            Size = PanelSize
        };
        AddChild(_background);

        _label = new Label
        {
            Position = new Vector2(5, 5)
        };
        _label.AddThemeColorOverride("font_color", TextColor);
        _label.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_label);

        Position = PanelPosition;
        Size = PanelSize;

        EventBus.Instance.Subscribe<FrameAdvancedEvent>(_OnFrameAdvanced);
        EventBus.Instance.Subscribe<HitConnectedEvent>(_OnHitConnected);
        EventBus.Instance.Subscribe<MoveBlockedEvent>(_OnMoveBlocked);

        _UpdateDisplay();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.Unsubscribe<FrameAdvancedEvent>(_OnFrameAdvanced);
        EventBus.Instance.Unsubscribe<HitConnectedEvent>(_OnHitConnected);
        EventBus.Instance.Unsubscribe<MoveBlockedEvent>(_OnMoveBlocked);
    }

    private void _OnFrameAdvanced(FrameAdvancedEvent e)
    {
        if (_advantage > 0)
            _advantage--;
        else if (_advantage < 0)
            _advantage++;
        _UpdateDisplay();
    }

    private void _OnHitConnected(HitConnectedEvent e)
    {
        if (e.AttackerId == TrackedPlayer)
            _advantage = e.HitAdvantage;
        else if (e.DefenderId == TrackedPlayer)
            _advantage = -e.HitAdvantage;
        else
            return;
        _UpdateDisplay();
    }

    private void _OnMoveBlocked(MoveBlockedEvent e)
    {
        if (e.AttackerId == TrackedPlayer)
            _advantage = e.BlockAdvantage;
        else if (e.DefenderId == TrackedPlayer)
            _advantage = -e.BlockAdvantage;
        else
            return;
        _UpdateDisplay();
    }

    private void _UpdateDisplay()
    {
        if (_label == null)
            return;

        var visible = _advantage != 0 || ShowWhenZero;
        _label.Visible = visible;
        if (_background != null)
            _background.Visible = visible;

        if (!visible)
            return;

        _label.Text = _advantage > 0 ? $"+{_advantage}" : $"{_advantage}";
    }
}
