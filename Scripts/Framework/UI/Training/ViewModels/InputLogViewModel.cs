#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class InputLogViewModel
{
    public readonly record struct DisplayEntry(int Frame, int PlayerId, InputType Type, int Value);

    private readonly List<DisplayEntry> _entries = new();
    private int _visibleRowCount = 10;
    private int _displayFrameLimit = int.MaxValue;
    private int _scrollOffset;
    private int _capacity = 600;

    public bool ShowP1 { get; set; } = true;
    public bool ShowP2 { get; set; } = true;

    public IInputLogFormatter Formatter { get; set; } = FrameLevelInputLogFormatter.Instance;

    public int VisibleRowCount
    {
        get => _visibleRowCount;
        set => _visibleRowCount = Math.Clamp(value, 1, 50);
    }

    public int DisplayFrameLimit
    {
        get => _displayFrameLimit;
        set => _displayFrameLimit = value;
    }

    public int ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            int maxOffset = Math.Max(0, DisplayRowCount - VisibleRowCount);
            _scrollOffset = Math.Clamp(value, 0, maxOffset);
        }
    }

    public int Capacity
    {
        get => _capacity;
        set => _capacity = Math.Max(1, value);
    }

    public IReadOnlyList<DisplayEntry> Entries => _entries.AsReadOnly();
    public int EntryCount => _entries.Count;

    public int FilteredCount
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

    public int MaxScrollOffset => Math.Max(0, DisplayRowCount - VisibleRowCount);

    // Row count in the formatter's output domain — a merging formatter produces
    // fewer rows than there are filtered entries. Scrolling operates on rows.
    public int DisplayRowCount => Formatter.FormatRows(GetFilteredEntries()).Count;

    public void LoadInitialSnapshot(IInputHistory? inputHistory, int trackedPlayer, bool showP1, bool showP2)
    {
        _entries.Clear();

        if (inputHistory == null)
            return;

        if (showP1 || trackedPlayer == 1)
        {
            foreach (var entry in inputHistory.GetDirectionalHistory(1))
                _entries.Add(new DisplayEntry(entry.Frame, 1, entry.Type, entry.Value));
            foreach (var entry in inputHistory.GetButtonHistory(1))
                _entries.Add(new DisplayEntry(entry.Frame, 1, entry.Type, entry.Value));
        }
        if (showP2 || trackedPlayer == 2)
        {
            foreach (var entry in inputHistory.GetDirectionalHistory(2))
                _entries.Add(new DisplayEntry(entry.Frame, 2, entry.Type, entry.Value));
            foreach (var entry in inputHistory.GetButtonHistory(2))
                _entries.Add(new DisplayEntry(entry.Frame, 2, entry.Type, entry.Value));
        }

        _entries.Sort((a, b) =>
        {
            int cmp = a.Frame.CompareTo(b.Frame);
            if (cmp != 0) return cmp;
            cmp = a.PlayerId.CompareTo(b.PlayerId);
            return cmp != 0 ? cmp : a.Type.CompareTo(b.Type);
        });

        TrimTrack(1, InputType.Directional);
        TrimTrack(1, InputType.Button);
        TrimTrack(2, InputType.Directional);
        TrimTrack(2, InputType.Button);
    }

    public bool TryAddEntry(InputReceivedEvent e)
    {
        if (e.PlayerId < 1 || e.PlayerId > 2)
            return false;
        if (!Enum.IsDefined(typeof(InputType), e.InputType))
            return false;

        var entry = new DisplayEntry(e.Frame, e.PlayerId, (InputType)e.InputType, e.InputValue);

        if (_entries.Contains(entry))
            return false;

        _entries.Add(entry);
        TrimTrack(e.PlayerId, entry.Type);
        return true;
    }

    public void RewindToFrame(int frameNumber)
    {
        _entries.RemoveAll(entry => entry.Frame > frameNumber);
    }

    public List<DisplayEntry> GetFilteredEntries()
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

    public List<string> GetVisibleRowTexts()
    {
        var rows = Formatter.FormatRows(GetFilteredEntries());
        int maxOffset = Math.Max(0, rows.Count - VisibleRowCount);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);

        int startIdx = Math.Max(0, rows.Count - VisibleRowCount - _scrollOffset);
        var visible = new List<string>(VisibleRowCount);

        for (int i = 0; i < VisibleRowCount; i++)
        {
            int rowIdx = startIdx + i;
            if (rowIdx < rows.Count)
                visible.Add(rows[rowIdx]);
            else
                visible.Add("");
        }

        return visible;
    }

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

    public static string FormatEntry(DisplayEntry entry)
    {
        string typeChar = entry.Type == InputType.Directional ? "D" : "B";
        string valueStr = FormatValue(entry.Type, entry.Value);
        return $"{entry.Frame,4}  P{entry.PlayerId}  {typeChar}  {valueStr}";
    }

    public static string FormatValue(InputType type, int value)
    {
        return type == InputType.Directional
            ? FormatDirection(value)
            : FormatButton(value);
    }

    public static string FormatDirection(int value)
    {
        return value is >= 1 and <= 9 ? value.ToString() : $"?{value}";
    }

    public static string FormatButton(int value)
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
