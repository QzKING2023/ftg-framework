#nullable enable
using FTG_Framework.UI.Training.ViewModels;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class ComboDisplay : Control
{
    [Export] public int TrackedAttacker { get; set; } = 1;
    [Export] public Vector2 PanelSize { get; set; } = new(240, 32);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 14;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    private ComboDisplayViewModel? _viewModel;
    private ComboDisplayController? _controller;
    private Label? _label;
    private ColorRect? _background;

    internal ComboDisplayViewModel? ViewModel => _viewModel;
    internal string DisplayText => _label?.Text ?? string.Empty;

    public override void _Ready()
    {
        if (_label is null)
            BuildControls();

        _controller?.Start();
        Render();
    }

    public override void _ExitTree() => Shutdown();

    public void Shutdown()
    {
        _controller?.Shutdown();
        Render();
    }

    public static string FormatDisplayText(int attackerId, ComboDisplayState state) =>
        $"P{attackerId} Combo: {state.HitCount} Hits | Damage: {state.TotalDamage}";

    private void BuildControls()
    {
        _viewModel = new ComboDisplayViewModel();
        _controller = new ComboDisplayController(_viewModel);
        _controller.StateChanged += Render;

        double scale = GetThemeDefaultBaseScale();
        CustomMinimumSize = PanelSize * (float)scale;
        _background = new ColorRect
        {
            Color = BackgroundColor,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = PanelSize
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        _label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None
        };
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label.OffsetLeft = 5;
        _label.OffsetTop = 5;
        _label.OffsetRight = -5;
        _label.OffsetBottom = -5;
        _label.AddThemeColorOverride("font_color", TextColor);
        _label.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_label);

        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
    }

    private void Render()
    {
        if (_label is null || _viewModel is null) return;
        _label.Text = FormatDisplayText(TrackedAttacker, _viewModel.GetState(TrackedAttacker));
    }
}
