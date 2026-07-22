#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.UI.Training;

public partial class InputLog : Control
{
    private int _visibleRowCount = 10;

    [Export] public int TrackedPlayer { get; set; } = 1;
    [Export] public bool ShowP1 { get; set; } = true;
    [Export] public bool ShowP2 { get; set; } = true;
    [Export]
    public int VisibleRowCount
    {
        get => _visibleRowCount;
        set
        {
            int clamped = Math.Clamp(value, 1, 50);
            if (clamped != value)
                GD.PushError($"[InputLog] VisibleRowCount must be 1-50, got {value}. Clamped to {clamped}.");
            if (_visibleRowCount == clamped)
                return;
            _visibleRowCount = clamped;
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

    public IInputHistory? InputHistory { get; set; }

    private int _displayFrameLimit = int.MaxValue;

    public int DisplayFrameLimit
    {
        get => _displayFrameLimit;
        set
        {
            if (_displayFrameLimit == value)
                return;
            _displayFrameLimit = value;
            _RefreshDisplay();
        }
    }

    internal readonly record struct DisplayEntry(int Frame, int PlayerId, InputType Type, int Value);

    private readonly List<DisplayEntry> _entries = new();
    private int _scrollOffset;
    private ColorRect? _background;
    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _vbox;
    private readonly List<Label> _rowLabels = new();

    internal IReadOnlyList<DisplayEntry> Entries => _entries.AsReadOnly();
    internal int EntryCount => _entries.Count;
    internal int Capacity => InputHistory?.Capacity ?? 600;

    public int ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            int maxOffset = Math.Max(0, FilteredCount - VisibleRowCount);
            _scrollOffset = Math.Clamp(value, 0, maxOffset);
            _RefreshDisplay();
        }
    }

    internal int FilteredCount
    {
        get
        {
            int count = 0;
            foreach (var e in _entries)
            {
                if (e.Frame > DisplayFrameLimit)
                    continue;
                if ((e.PlayerId == 1 && ShowP1) || (e.PlayerId == 2 && ShowP2))
                    count++;
            }
            return count;
        }
    }

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
        // EventBus.Subscribe dedupes handlers; subscribing before the re-entry guard
        // keeps the panel live when it re-enters the tree after _ExitTree.
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

    // Inputs recorded on the abandoned future branch must not linger in the log
    // (they would resurrect on resume). Mirrors InputHistory's rollback;
    // re-executed frames re-arrive via InputReceivedEvent.
    private void _OnFrameRewound(FrameRewoundEvent e)
    {
        _entries.RemoveAll(entry => entry.Frame > e.FrameNumber);
        _RefreshDisplay();
    }

    private void CreateRowLabels()
    {
        for (int i = 0; i < _visibleRowCount; i++)
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
        if (InputHistory == null)
            return;

        _entries.Clear();

        // The tracked player's history loads even when its display toggle is off,
        // so enabling the toggle later doesn't reveal an incomplete log.
        if (ShowP1 || TrackedPlayer == 1)
            AppendHistory(1);
        if (ShowP2 || TrackedPlayer == 2)
            AppendHistory(2);

        _entries.Sort((a, b) =>
        {
            int cmp = a.Frame.CompareTo(b.Frame);
            if (cmp != 0) return cmp;
            cmp = a.PlayerId.CompareTo(b.PlayerId);
            return cmp != 0 ? cmp : a.Type.CompareTo(b.Type);
        });

        // Enforce the storage invariant regardless of the IInputHistory implementation.
        TrimTrack(1, InputType.Directional);
        TrimTrack(1, InputType.Button);
        TrimTrack(2, InputType.Directional);
        TrimTrack(2, InputType.Button);
    }

    private void AppendHistory(int playerId)
    {
        foreach (var entry in InputHistory!.GetDirectionalHistory(playerId))
            _entries.Add(new DisplayEntry(entry.Frame, playerId, entry.Type, entry.Value));
        foreach (var entry in InputHistory.GetButtonHistory(playerId))
            _entries.Add(new DisplayEntry(entry.Frame, playerId, entry.Type, entry.Value));
    }

    private void _OnInputReceived(InputReceivedEvent e)
    {
        if (e.PlayerId < 1 || e.PlayerId > 2)
        {
            GD.PushError($"[InputLog] Ignoring input with invalid playerId {e.PlayerId}.");
            return;
        }
        if (!Enum.IsDefined(typeof(InputType), e.InputType))
        {
            GD.PushError($"[InputLog] Ignoring input with invalid InputType {e.InputType}.");
            return;
        }

        var entry = new DisplayEntry(e.Frame, e.PlayerId, (InputType)e.InputType, e.InputValue);

        // RecordInput writes to storage synchronously but the event dispatches on the
        // next ProcessFrame, so a panel readied in between would see this entry twice.
        if (_entries.Contains(entry))
            return;

        _entries.Add(entry);
        TrimTrack(e.PlayerId, entry.Type);
        _RefreshDisplay();
    }

    // Mirrors InputHistory's per-(player, type) CircularBuffer: keep the newest
    // Capacity entries of the track, evicting oldest first.
    private void TrimTrack(int playerId, InputType type)
    {
        int excess = -Capacity;
        foreach (var e in _entries)
            if (e.PlayerId == playerId && e.Type == type)
                excess++;
        for (int i = 0; i < _entries.Count && excess > 0;)
        {
            if (_entries[i].PlayerId == playerId && _entries[i].Type == type)
            {
                _entries.RemoveAt(i);
                excess--;
            }
            else
            {
                i++;
            }
        }
    }

    internal void _RefreshDisplay()
    {
        if (_rowLabels.Count == 0)
            return;

        var filtered = GetFilteredEntries();
        int maxOffset = Math.Max(0, filtered.Count - VisibleRowCount);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);

        // Offset 0 anchors to the newest entries (bottom of the log);
        // increasing the offset scrolls up into older history.
        int startIdx = Math.Max(0, filtered.Count - VisibleRowCount - _scrollOffset);

        for (int i = 0; i < VisibleRowCount; i++)
        {
            int entryIdx = startIdx + i;
            if (entryIdx < filtered.Count)
            {
                var entry = filtered[entryIdx];
                _rowLabels[i].Text = FormatEntry(entry);
                _rowLabels[i].Visible = true;
            }
            else
            {
                _rowLabels[i].Text = "";
                _rowLabels[i].Visible = false;
            }
        }
    }

    private List<DisplayEntry> GetFilteredEntries()
    {
        var filtered = new List<DisplayEntry>();
        foreach (var entry in _entries)
        {
            if (entry.Frame > DisplayFrameLimit)
                continue;
            if (ShowP1 && ShowP2)
            {
                filtered.Add(entry);
                continue;
            }
            if ((entry.PlayerId == 1 && ShowP1) || (entry.PlayerId == 2 && ShowP2))
                filtered.Add(entry);
        }
        return filtered;
    }

    internal static string FormatEntry(DisplayEntry entry)
    {
        string typeChar = entry.Type == InputType.Directional ? "D" : "B";
        string valueStr = FormatValue(entry.Type, entry.Value);
        return $"{entry.Frame,4}  P{entry.PlayerId}  {typeChar}  {valueStr}";
    }

    internal static string FormatValue(InputType type, int value)
    {
        return type == InputType.Directional
            ? FormatDirection(value)
            : FormatButton(value);
    }

    internal static string FormatDirection(int value)
    {
        // DirectionValue enum uses numpad values directly: 1=DownBack, 2=Down,
        // 3=DownForward, 4=Back, 5=Neutral, 6=Forward, 7=UpBack, 8=Up, 9=UpForward.
        // Valid range is 1-9. Everything else gets a fallback prefix.
        return value is >= 1 and <= 9 ? value.ToString() : $"?{value}";
    }

    internal static string FormatButton(int value)
    {
        return value switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            3 => "D",
            _ => $"?{value}"
        };
    }
}
