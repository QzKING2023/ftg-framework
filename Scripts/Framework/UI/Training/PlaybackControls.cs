#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public partial class PlaybackControls : Control
{
    [Export] public Vector2 PanelPosition { get; set; } = new(400, 10);
    [Export] public Vector2 PanelSize { get; set; } = new(200, 40);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 12;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    public IFrameDataEngine? FrameDataEngine
    {
        get => _vm.FrameDataEngine;
        set => _vm.FrameDataEngine = value;
    }

    public InputLog? InputLog { get; set; }

    private readonly PlaybackControlsViewModel _vm = new();
    private Label? _label;
    private ColorRect? _background;

    internal PlaybackControlsViewModel ViewModel => _vm;
    public int DisplayFrame => _vm.DisplayFrame;
    internal string DisplayText => _label?.Text ?? string.Empty;
    internal bool IsLabelVisible => _label?.Visible ?? false;
    internal bool IsBackgroundVisible => _background?.Visible ?? false;

    public bool Paused
    {
        get => _vm.Paused;
        set
        {
            _vm.SetPaused(value);
            UpdateLabel();
        }
    }

    public override void _Ready()
    {
        EventBus.Instance.Subscribe<FrameAdvancedEvent>(_OnFrameAdvanced);
        _vm.DisplayFrameLimitChanged = OnDisplayFrameLimitChanged;

        if (_label != null)
            return;

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

        UpdateLabel();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.Unsubscribe<FrameAdvancedEvent>(_OnFrameAdvanced);
        _vm.OnExitTree();
    }

    private void OnDisplayFrameLimitChanged(int limit)
    {
        if (InputLog is not null)
            InputLog.DisplayFrameLimit = limit;
    }

    private void _OnFrameAdvanced(FrameAdvancedEvent e)
    {
        _vm.OnFrameAdvanced(e.FrameNumber);
        UpdateLabel();
    }

    public void TogglePause()
    {
        _vm.TogglePause();
        UpdateLabel();
    }

    public void StepForward()
    {
        _vm.StepForward();
        UpdateLabel();
    }

    public void StepBackward()
    {
        _vm.StepBackward();
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_label == null)
            return;
        _label.Text = _vm.DisplayText;
    }
}
