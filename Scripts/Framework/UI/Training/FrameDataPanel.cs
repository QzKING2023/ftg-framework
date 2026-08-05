#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public partial class FrameDataPanel : Control
{
    [Export] public int TrackedPlayer { get; set; } = 1;
    [Export] public Vector2 PanelSize { get; set; } = new(300, 60);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 14;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    public IDataStore? DataStore { get; set; }

    private readonly FrameDataViewModel _vm = new();
    private Label? _infoLabel;
    private Label? _durationsLabel;
    private ColorRect? _background;

    internal FrameDataViewModel ViewModel => _vm;
    internal string InfoText => _infoLabel?.Text ?? string.Empty;
    internal string DurationsText => _durationsLabel?.Text ?? string.Empty;
    internal bool DurationsVisible => _durationsLabel?.Visible ?? false;

    public override void _Ready()
    {
        CustomMinimumSize = PanelSize * (float)GetThemeDefaultBaseScale();
        _background = new ColorRect
        {
            Color = BackgroundColor
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        _infoLabel = new Label
        {
            Position = new Vector2(5, 5)
        };
        _infoLabel.AddThemeColorOverride("font_color", TextColor);
        _infoLabel.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_infoLabel);

        _durationsLabel = new Label
        {
            Position = new Vector2(5, 25)
        };
        _durationsLabel.AddThemeColorOverride("font_color", TextColor);
        _durationsLabel.AddThemeFontSizeOverride("font_size", FontSize);
        AddChild(_durationsLabel);

        EventBus.Instance.Subscribe<MoveFrameChangedEvent>(_OnMoveFrameChanged);

        _UpdateDisplay(null, MovePhase.Idle, 0, 0);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.Unsubscribe<MoveFrameChangedEvent>(_OnMoveFrameChanged);
    }

    private void _OnMoveFrameChanged(MoveFrameChangedEvent e)
    {
        if (e.PlayerId == TrackedPlayer)
            _UpdateDisplay(e.MoveId, e.Phase, e.CurrentFrame, e.TotalFrames);
    }

    private void _UpdateDisplay(string? moveId, MovePhase phase, int currentFrame, int totalFrames)
    {
        if (_infoLabel == null || _durationsLabel == null)
            return;

        _vm.Update(DataStore, moveId, phase, currentFrame, totalFrames);

        _infoLabel.Text = _vm.InfoText;
        _infoLabel.Visible = true;
        _durationsLabel.Visible = _vm.DurationsVisible;
        if (_vm.DurationsVisible)
            _durationsLabel.Text = _vm.DurationsText;
    }
}
