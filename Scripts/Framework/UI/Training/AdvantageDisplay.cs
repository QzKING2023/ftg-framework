#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;

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

    private AdvantageViewModel? _vm;
    private Label? _label;
    private ColorRect? _background;

    internal AdvantageViewModel? ViewModel => _vm;
    internal int Advantage => _vm?.Advantage ?? 0;
    internal string DisplayText => _label?.Text ?? string.Empty;
    internal bool IsLabelVisible => _label?.Visible ?? false;
    internal bool IsBackgroundVisible => _background?.Visible ?? false;

    public override void _Ready()
    {
        if (_label != null)
            return;

        if (TrackedPlayer < 1 || TrackedPlayer > 2)
            GD.PushError($"[AdvantageDisplay] TrackedPlayer must be 1 or 2, got {TrackedPlayer}. Display will remain inert.");

        _vm = new AdvantageViewModel(TrackedPlayer, ShowWhenZero);

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

        _Render();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.Unsubscribe<FrameAdvancedEvent>(_OnFrameAdvanced);
        EventBus.Instance.Unsubscribe<HitConnectedEvent>(_OnHitConnected);
        EventBus.Instance.Unsubscribe<MoveBlockedEvent>(_OnMoveBlocked);
    }

    private void _OnFrameAdvanced(FrameAdvancedEvent e)
    {
        _vm?.OnFrameAdvanced();
        _Render();
    }

    private void _OnHitConnected(HitConnectedEvent e)
    {
        _vm?.OnHitConnected(e.AttackerId, e.DefenderId, e.HitAdvantage);
        _Render();
    }

    private void _OnMoveBlocked(MoveBlockedEvent e)
    {
        _vm?.OnMoveBlocked(e.AttackerId, e.DefenderId, e.BlockAdvantage);
        _Render();
    }

    private void _Render()
    {
        if (_label == null || _vm == null)
            return;

        _label.Visible = _vm.IsVisible;
        if (_background != null)
            _background.Visible = _vm.IsVisible;

        if (_vm.IsVisible)
            _label.Text = _vm.DisplayText;
    }
}
