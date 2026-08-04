#nullable enable
using FTG_Framework.UI.Training.ViewModels;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class ComboDisplay : Control
{
    [Export] public int TrackedAttacker { get; set; } = 1;
    [Export] public Vector2 PanelPosition { get; set; } = new(10, 110);
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

        _background = new ColorRect
        {
            Color = BackgroundColor,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = PanelSize,
            Size = PanelSize
        };
        AddChild(_background);

        _label = new Label
        {
            Position = new Vector2(5, 5),
            Size = PanelSize - new Vector2(10, 10),
            CustomMinimumSize = PanelSize - new Vector2(10, 10),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None
        };
        _label.AddThemeColorOverride("font_color", TextColor);
        _label.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_label);

        Position = PanelPosition;
        Size = PanelSize;
        CustomMinimumSize = PanelSize;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
    }

    private void Render()
    {
        if (_label is null || _viewModel is null) return;
        _label.Text = FormatDisplayText(TrackedAttacker, _viewModel.GetState(TrackedAttacker));
    }
}
