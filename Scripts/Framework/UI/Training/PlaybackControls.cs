#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.UI.Training;

public partial class PlaybackControls : Control
{
    [Export] public Vector2 PanelPosition { get; set; } = new(400, 10);
    [Export] public Vector2 PanelSize { get; set; } = new(200, 40);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 12;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    public IFrameDataEngine? FrameDataEngine { get; set; }
    public InputLog? InputLog { get; set; }

    private int _displayFrame;
    private Label? _label;
    private ColorRect? _background;

    public int DisplayFrame => _displayFrame;
    internal string DisplayText => _label?.Text ?? string.Empty;
    internal bool IsLabelVisible => _label?.Visible ?? false;
    internal bool IsBackgroundVisible => _background?.Visible ?? false;

    // EventBus.Paused is the single source of truth — an external writer and this
    // panel can never diverge.
    public bool Paused
    {
        get => EventBus.Instance.Paused;
        set
        {
            EventBus.Instance.Paused = value;
            if (!value)
                InputLogSetDisplayFrameLimit(int.MaxValue);
            UpdateLabel();
        }
    }

    public override void _Ready()
    {
        // EventBus.Subscribe dedupes handlers; subscribing before the re-entry guard
        // keeps the panel live when it re-enters the tree after _ExitTree.
        EventBus.Instance.Subscribe<FrameAdvancedEvent>(_OnFrameAdvanced);

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
        // A freed panel must not strand the game in paused state with no UI to resume.
        if (EventBus.Instance.Paused)
        {
            InputLogSetDisplayFrameLimit(int.MaxValue);
            EventBus.Instance.Paused = false;
            EventBus.Instance.StepRequested = false;
        }
    }

    private void _OnFrameAdvanced(FrameAdvancedEvent e)
    {
        // One frame domain: RestoreFrame rewinds the bus counter, so the event's
        // frame is always the displayed position — paused stepping included.
        _displayFrame = e.FrameNumber;
        // While paused, keep the input log pinned to the inspected frame; on
        // resume the Paused setter lifts the limit back to int.MaxValue.
        if (Paused)
            InputLogSetDisplayFrameLimit(_displayFrame);
        UpdateLabel();
    }

    public void TogglePause()
    {
        Paused = !Paused;
    }

    public void StepForward()
    {
        if (!Paused)
            Paused = true;
        EventBus.Instance.StepRequested = true;
    }

    public void StepBackward()
    {
        if (!Paused)
            Paused = true;
        if (_displayFrame <= 0)
            return;
        if (FrameDataEngine is null)
            return;
        if (FrameDataEngine.EarliestSnapshotFrame >= 0 && _displayFrame - 1 < FrameDataEngine.EarliestSnapshotFrame)
            return;

        if (!FrameDataEngine.RestoreFrame(_displayFrame - 1))
            return;

        _displayFrame -= 1;
        InputLogSetDisplayFrameLimit(_displayFrame);
        UpdateLabel();
    }

    private void InputLogSetDisplayFrameLimit(int limit)
    {
        if (InputLog is not null)
            InputLog.DisplayFrameLimit = limit;
    }

    private void UpdateLabel()
    {
        if (_label == null)
            return;

        string state = Paused ? "PAUSED" : "RUNNING";
        _label.Text = $"Frame: {_displayFrame} [{state}]";
    }
}
