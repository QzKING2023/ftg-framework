#nullable enable
using Godot;
using FTG_Framework.Core;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public partial class EventBusDebugPanel : Control
{
    [Export] public Vector2 PanelSize { get; set; } = new(400, 500);
    [Export] public int FontSize { get; set; } = 12;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.7f);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public Color SelectedColor { get; set; } = new(0.3f, 0.5f, 0.8f, 0.5f);

    private readonly EventBusDebugService _service = new();
    private readonly EventBusDebugViewModel _vm;
    private VBoxContainer? _container;
    private Label? _titleLabel;
    private Label? _entryListLabel;
    private Label? _detailLabel;
    private ScrollContainer? _scrollContainer;
    private bool _prevToggleKey;

    /// <summary>Hosted-toolbox affordance (S4.3-AC06): no BackQuote runtime toggle;
    /// visibility and service activation are driven by the workspace.</summary>
    internal bool HostedMode { get; set; }

    internal EventBusDebugViewModel ViewModel => _vm;
    internal EventBusDebugService Service => _service;
    internal string TitleText => _titleLabel?.Text ?? string.Empty;
    internal string EntryListText => _entryListLabel?.Text ?? string.Empty;
    internal string DetailText => _detailLabel?.Text ?? string.Empty;

    public EventBusDebugPanel()
    {
        _vm = new EventBusDebugViewModel(_service);
    }

    public override void _Ready()
    {
        CustomMinimumSize = PanelSize * (float)GetThemeDefaultBaseScale();
        var background = new ColorRect
        {
            Color = BackgroundColor
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _container = new VBoxContainer();
        _container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _container.OffsetLeft = 5;
        _container.OffsetTop = 5;
        _container.OffsetRight = -5;
        _container.OffsetBottom = -5;
        AddChild(_container);

        _titleLabel = new Label { Text = "EventBus Debug Panel (PageUp/PageDown navigate)" };
        _titleLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        _titleLabel.AddThemeFontSizeOverride("font_size", FontSize + 2);
        _container.AddChild(_titleLabel);

        var controlsRow = new HBoxContainer();
        var sortButton = new Button { Text = "Sort: recency" };
        sortButton.Pressed += () =>
        {
            _vm.CycleSortMode();
            ((Button)sortButton).Text = "Sort: " + _vm.SortMode;
            _vm.SelectedIndex = -1;
        };
        controlsRow.AddChild(sortButton);

        if (!HostedMode)
        {
            var closeButton = new Button { Text = "Close (Toggle: `)" };
            closeButton.Pressed += () =>
            {
                _vm.ToggleVisibility();
                Visible = _vm.IsVisible;
            };
            controlsRow.AddChild(closeButton);
        }
        _container.AddChild(controlsRow);

        _scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(PanelSize.X - 10, PanelSize.Y * 0.55f)
        };
        _container.AddChild(_scrollContainer);

        _entryListLabel = new Label();
        _entryListLabel.AddThemeColorOverride("font_color", TextColor);
        _entryListLabel.AddThemeFontSizeOverride("font_size", FontSize);
        _scrollContainer.AddChild(_entryListLabel);

        _detailLabel = new Label();
        _detailLabel.AddThemeColorOverride("font_color", TextColor);
        _detailLabel.AddThemeFontSizeOverride("font_size", FontSize);
        _detailLabel.SizeFlagsVertical |= Control.SizeFlags.ExpandFill;
        _container.AddChild(_detailLabel);

        // Start hidden in standalone mode — user toggles via BackQuote key.
        // In hosted mode the workspace owns visibility and activation.
        if (!HostedMode)
            Visible = false;
    }

    /// <summary>
    /// Hosted activation (S4.3-AC06): drives the ViewModel's visibility state,
    /// which enables/disables the underlying EventBusDebugService exactly once
    /// per transition. No-op when already in the requested state.
    /// </summary>
    internal void SetActive(bool active)
    {
        if (active == _vm.IsVisible)
            return;
        _vm.ToggleVisibility();
    }

    public override void _Process(double delta)
    {
        // A hidden hosted panel owns no active callbacks (S4.3-AC06).
        if (HostedMode && !Visible)
            return;

        if (!HostedMode)
        {
            // Toggle key: BackQuote (tilde) — standalone behavior unchanged
            bool toggleKey = Godot.Input.IsKeyPressed(Key.Quoteleft);
            if (toggleKey && !_prevToggleKey)
            {
                _vm.ToggleVisibility();
                Visible = _vm.IsVisible;
            }
            _prevToggleKey = toggleKey;
        }

        if (!_vm.IsVisible)
            return;

        _vm.Refresh();

        if (_entryListLabel == null || _detailLabel == null)
            return;

        // Build entry list text
        var entries = _vm.SortedEntries;
        var entryLines = new System.Text.StringBuilder();
        int idx = 0;
        foreach (var entry in entries)
        {
            string line = idx switch
            {
                _ when idx == _vm.SelectedIndex => $"> [{entry.EventTypeName}] subs:{entry.SubscriberCount} f:{entry.LastFrameSeen} n:{entry.TotalOccurrences}\n",
                _ => $"  [{entry.EventTypeName}] subs:{entry.SubscriberCount} f:{entry.LastFrameSeen} n:{entry.TotalOccurrences}\n"
            };
            entryLines.Append(line);
            idx++;
        }

        _entryListLabel.Text = entryLines.ToString();

        // Handle navigation input
        if (Godot.Input.IsKeyPressed(Key.Pagedown) && !_prevDown)
            _vm.SelectedIndex = _vm.SelectedIndex + 1 < entries.Count ? _vm.SelectedIndex + 1 : _vm.SelectedIndex;
        if (Godot.Input.IsKeyPressed(Key.Pageup) && !_prevUp)
            _vm.SelectedIndex = _vm.SelectedIndex - 1 >= -1 ? _vm.SelectedIndex - 1 : _vm.SelectedIndex;

        _detailLabel.Text = _vm.SelectedDetailText;

        _prevDown = Godot.Input.IsKeyPressed(Key.Pagedown);
        _prevUp = Godot.Input.IsKeyPressed(Key.Pageup);
    }

    private bool _prevDown;
    private bool _prevUp;

    public override void _ExitTree()
    {
        _service.Disable();
    }
}
