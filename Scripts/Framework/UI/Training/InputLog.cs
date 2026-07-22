#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public partial class InputLog : Control
{
    [Export] public int TrackedPlayer { get; set; } = 1;
    [Export] public bool ShowP1
    {
        get => _vm.ShowP1;
        set { _vm.ShowP1 = value; _RefreshDisplay(); }
    }
    [Export] public bool ShowP2
    {
        get => _vm.ShowP2;
        set { _vm.ShowP2 = value; _RefreshDisplay(); }
    }
    [Export]
    public int VisibleRowCount
    {
        get => _vm.VisibleRowCount;
        set
        {
            int clamped = Math.Clamp(value, 1, 50);
            if (clamped != value)
                GD.PushError($"[InputLog] VisibleRowCount must be 1-50, got {value}. Clamped to {clamped}.");
            if (_vm.VisibleRowCount == clamped)
                return;
            _vm.VisibleRowCount = clamped;
            if (_vbox != null)
            {
                RebuildRowLabels();
                _RefreshDisplay();
            }
        }
    }
    [Export] public Vector2 PanelPosition { get; set; } = new(10, 110);
    [Export] public Vector2 PanelSize { get; set; } = new(280, 180);
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public int FontSize { get; set; } = 12;
    [Export] public Color BackgroundColor { get; set; } = new(0, 0, 0, 0.5f);

    public IInputHistory? InputHistory
    {
        get => _inputHistory;
        set
        {
            _inputHistory = value;
            _vm.Capacity = value?.Capacity ?? 600;
        }
    }

    public int DisplayFrameLimit
    {
        get => _vm.DisplayFrameLimit;
        set
        {
            if (_vm.DisplayFrameLimit == value)
                return;
            _vm.DisplayFrameLimit = value;
            _RefreshDisplay();
        }
    }

    private readonly InputLogViewModel _vm = new();
    private IInputHistory? _inputHistory;
    private ColorRect? _background;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _vbox;
    private readonly List<Label> _rowLabels = new();

    internal InputLogViewModel ViewModel => _vm;
    internal IReadOnlyList<InputLogViewModel.DisplayEntry> Entries => _vm.Entries;
    internal int EntryCount => _vm.EntryCount;
    internal int Capacity => _vm.Capacity;

    public int ScrollOffset
    {
        get => _vm.ScrollOffset;
        set
        {
            _vm.ScrollOffset = value;
            _RefreshDisplay();
        }
    }

    internal int FilteredCount => _vm.FilteredCount;

    internal string DisplayText => string.Join("\n",
        _rowLabels.ConvertAll(l => l.Text));

    internal int VisibleLabelCount
    {
        get
        {
            int count = 0;
            foreach (var l in _rowLabels)
                if (l.Visible) count++;
            return count;
        }
    }

    internal bool IsBackgroundVisible => _background?.Visible ?? false;

    public override void _Ready()
    {
        EventBus.Instance.Subscribe<InputReceivedEvent>(_OnInputReceived);
        EventBus.Instance.Subscribe<FrameRewoundEvent>(_OnFrameRewound);

        if (_rowLabels.Count > 0)
            return;

        if (TrackedPlayer < 1 || TrackedPlayer > 2)
            GD.PushError($"[InputLog] TrackedPlayer must be 1 or 2, got {TrackedPlayer}.");

        _background = new ColorRect
        {
            Color = BackgroundColor,
            Size = PanelSize
        };
        AddChild(_background);

        _scrollContainer = new ScrollContainer
        {
            Position = Vector2.Zero,
            Size = PanelSize
        };
        AddChild(_scrollContainer);

        _vbox = new VBoxContainer();
        _scrollContainer.AddChild(_vbox);

        CreateRowLabels();

        Position = PanelPosition;
        Size = PanelSize;

        _LoadInitialSnapshot();
        _RefreshDisplay();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.Unsubscribe<InputReceivedEvent>(_OnInputReceived);
        EventBus.Instance.Unsubscribe<FrameRewoundEvent>(_OnFrameRewound);
    }

    private void _OnFrameRewound(FrameRewoundEvent e)
    {
        _vm.RewindToFrame(e.FrameNumber);
        _RefreshDisplay();
    }

    private void CreateRowLabels()
    {
        for (int i = 0; i < _vm.VisibleRowCount; i++)
        {
            var label = new Label();
            label.AddThemeColorOverride("font_color", TextColor);
            label.AddThemeFontSizeOverride("font_size", FontSize);
            _vbox!.AddChild(label);
            _rowLabels.Add(label);
        }
    }

    private void RebuildRowLabels()
    {
        foreach (var label in _rowLabels)
        {
            _vbox!.RemoveChild(label);
            label.QueueFree();
        }
        _rowLabels.Clear();
        CreateRowLabels();
    }

    private void _LoadInitialSnapshot()
    {
        _vm.LoadInitialSnapshot(_inputHistory, TrackedPlayer, ShowP1, ShowP2);
    }

    private void _OnInputReceived(InputReceivedEvent e)
    {
        if (_vm.TryAddEntry(e))
            _RefreshDisplay();
    }

    internal void _RefreshDisplay()
    {
        if (_rowLabels.Count == 0)
            return;

        var rows = _vm.GetVisibleRowTexts();

        for (int i = 0; i < _rowLabels.Count; i++)
        {
            if (i < rows.Count && rows[i].Length > 0)
            {
                _rowLabels[i].Text = rows[i];
                _rowLabels[i].Visible = true;
            }
            else
            {
                _rowLabels[i].Text = "";
                _rowLabels[i].Visible = false;
            }
        }
    }

    // Formatting delegates to the ViewModel (kept here for backward compat with existing callers/tests)
    internal static string FormatEntry(InputLogViewModel.DisplayEntry entry)
        => InputLogViewModel.FormatEntry(entry);

    internal static string FormatDirection(int value)
        => InputLogViewModel.FormatDirection(value);

    internal static string FormatButton(int value)
        => InputLogViewModel.FormatButton(value);
}
