#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.UI.Training;

public partial class FrameDataPanel : Control
{
    [Export] public int TrackedPlayer { get; set; } = 1;
    [Export] public Vector2 PanelPosition { get; set; } = new(10, 10);
    [Export] public Vector2 PanelSize { get; set; } = new(300, 60);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 14;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    public IDataStore? DataStore { get; set; }

    private Label? _infoLabel;
    private Label? _durationsLabel;
    private ColorRect? _background;

    internal string InfoText => _infoLabel?.Text ?? string.Empty;
    internal string DurationsText => _durationsLabel?.Text ?? string.Empty;
    internal bool DurationsVisible => _durationsLabel?.Visible ?? false;

    public override void _Ready()
    {
        _background = new ColorRect
        {
            Color = BackgroundColor,
            Size = PanelSize
        };
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

        Position = PanelPosition;
        Size = PanelSize;

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

        if (phase == MovePhase.Idle)
        {
            _infoLabel.Text = "Move: Idle";
            _infoLabel.Visible = true;
            _durationsLabel.Visible = false;
            return;
        }

        var moveDef = DataStore?.GetMove(moveId ?? string.Empty);
        if (moveDef == null)
        {
            _infoLabel.Text = $"Move: {moveId} (unknown) | Phase: {phase} | Frame: {currentFrame}/{totalFrames}";
            _durationsLabel.Visible = false;
            return;
        }

        int withinPhase = phase switch
        {
            MovePhase.Startup => currentFrame,
            MovePhase.Active => currentFrame - moveDef.Startup,
            MovePhase.Recovery => currentFrame - moveDef.Startup - moveDef.Active,
            _ => 0
        };

        int phaseTotal = phase switch
        {
            MovePhase.Startup => moveDef.Startup,
            MovePhase.Active => moveDef.Active,
            MovePhase.Recovery => moveDef.Recovery,
            _ => 0
        };

        _infoLabel.Text = $"Move: {moveId} | Phase: {phase} | Frame: {withinPhase}/{phaseTotal}";
        _infoLabel.Visible = true;

        _durationsLabel.Text = $"Startup: {moveDef.Startup}f | Active: {moveDef.Active}f | Recovery: {moveDef.Recovery}f";
        _durationsLabel.Visible = true;
    }
}
