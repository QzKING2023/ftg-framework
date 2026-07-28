#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class EventBusDebugViewModel
{
    private readonly Core.EventBusDebugService _service;

    public EventBusDebugViewModel(Core.EventBusDebugService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public bool IsVisible { get; set; }
    public string SortMode { get; set; } = "recency";
    public int SelectedIndex { get; set; } = -1;

    public IReadOnlyList<Core.EventDebugEntry> SortedEntries
    {
        get
        {
            var entries = _service.GetEntries();
            return SortMode switch
            {
                "name" => entries.OrderBy(e => e.EventTypeName).ToList(),
                "count" => entries.OrderByDescending(e => e.TotalOccurrences).ThenBy(e => e.EventTypeName).ToList(),
                _ => entries // "recency" — already sorted by service
            };
        }
    }

    public Core.EventDebugEntry? SelectedEntry
    {
        get
        {
            var sorted = SortedEntries;
            if (SelectedIndex < 0 || SelectedIndex >= sorted.Count)
                return null;
            return sorted[SelectedIndex];
        }
    }

    public string SelectedDetailText
    {
        get
        {
            var entry = SelectedEntry;
            if (entry == null)
                return "No event selected";

            var rateNote = entry.EventTypeName == nameof(FTG_Framework.Core.Events.FrameAdvancedEvent)
                ? "(rate-limited: updates every 15 frames)"
                : "";

            return $"Event: {entry.EventTypeName}\n"
                + $"Subscribers: {entry.SubscriberCount}\n"
                + $"Last Frame: {entry.LastFrameSeen}\n"
                + $"Total Occurrences: {entry.TotalOccurrences}\n"
                + $"Last Seen: {entry.LastPayloadTimestamp:HH:mm:ss.fff}\n"
                + $"{rateNote}\n"
                + $"--- Payload ---\n"
                + $"{FormatPayload(entry.LastPayloadSnapshot)}";
        }
    }

    public void Refresh()
    {
        // Queries service, properties recompute on access
    }

    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
        if (IsVisible)
            _service.Enable();
        else
            _service.Disable();
    }

    public void CycleSortMode()
    {
        SortMode = SortMode switch
        {
            "recency" => "name",
            "name" => "count",
            "count" => "recency",
            _ => "recency"
        };
        SelectedIndex = -1;
    }

    public static string FormatPayload(string rawPayload)
    {
        if (string.IsNullOrEmpty(rawPayload))
            return "(empty)";

        const int maxLineLength = 120;
        const int maxLines = 8;

        var lines = new List<string>();
        int pos = 0;
        while (pos < rawPayload.Length && lines.Count < maxLines)
        {
            int len = Math.Min(maxLineLength, rawPayload.Length - pos);
            lines.Add(rawPayload.Substring(pos, len));
            pos += len;
        }

        if (pos < rawPayload.Length)
            lines.Add("... (truncated)");

        return string.Join("\n", lines);
    }
}
