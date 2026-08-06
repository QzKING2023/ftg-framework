#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FTG_Framework.Editor;

/// <summary>
/// Versioned workspace-layout value (S4.3-AC08). The layout is a UI preference,
/// not a data/save/replay container (gate item 5): serialization and fallback
/// logic live here in pure C#; persistence through EditorSettings stays in the
/// thin adapter. Invalid or incompatible layout data falls back to the
/// documented default without affecting move/runtime data.
///
/// Format: "ftg-toolbox-layout/v1;Kind=visible,order,size;..." where visible is
/// 0/1, order is a positive integer, and size is a 0..1 proportion. The whole
/// value is strict: any malformed entry, unknown kind, or missing kind yields
/// the documented default.
/// </summary>
public sealed class ToolboxLayoutService
{
    public const int CurrentVersion = 1;
    public const string FormatPrefix = "ftg-toolbox-layout/v1";
    public const string EditorSettingsKey = "ftg_framework/toolbox_layout_v1";

    public static WorkspaceLayoutData DefaultLayout() => new(CurrentVersion, new[]
    {
        new ToolboxPanelLayoutEntry(ToolboxPanelKind.MoveAuthoring, Visible: true, Order: 1, SizeProportion: 0.50f),
        new ToolboxPanelLayoutEntry(ToolboxPanelKind.EventBusDebug, Visible: true, Order: 2, SizeProportion: 0.25f),
        new ToolboxPanelLayoutEntry(ToolboxPanelKind.RuntimeTuning, Visible: true, Order: 3, SizeProportion: 0.25f)
    });

    public static string Serialize(WorkspaceLayoutData layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var entries = layout.Panels
            .OrderBy(entry => entry.Order)
            .Select(entry =>
                $"{entry.Kind}={(entry.Visible ? 1 : 0)},{entry.Order},{entry.SizeProportion.ToString("0.00", CultureInfo.InvariantCulture)}");
        return $"{FormatPrefix};{string.Join(";", entries)}";
    }

    public static WorkspaceLayoutData Parse(string? serialized)
    {
        if (serialized is null) return DefaultLayout();
        string[] segments = serialized.Split(';');
        if (segments.Length == 0 || !string.Equals(segments[0], FormatPrefix, StringComparison.Ordinal))
            return DefaultLayout();

        var parsed = new Dictionary<ToolboxPanelKind, ToolboxPanelLayoutEntry>();
        for (int i = 1; i < segments.Length; i++)
        {
            if (!TryParseEntry(segments[i], out ToolboxPanelKind kind, out ToolboxPanelLayoutEntry entry))
                return DefaultLayout();
            if (!parsed.TryAdd(kind, entry))
                return DefaultLayout(); // duplicate kind
        }

        if (parsed.Count != 3) return DefaultLayout(); // every kind required
        if (parsed.Keys.ToHashSet().Count != 3) return DefaultLayout();
        if (parsed.Values.Select(entry => entry.Order).Distinct().Count() != 3) return DefaultLayout();
        float totalSize = parsed.Values.Sum(entry => entry.SizeProportion);
        if (totalSize < 0.001f || totalSize > 1.001f) return DefaultLayout();
        return new WorkspaceLayoutData(CurrentVersion,
            parsed.Values.OrderBy(entry => entry.Order).ToArray());
    }

    public static ToolboxPanelLayoutEntry EntryFor(WorkspaceLayoutData layout, ToolboxPanelKind kind) =>
        layout.Panels.First(entry => entry.Kind == kind);

    public static bool SameLayout(WorkspaceLayoutData left, WorkspaceLayoutData right)
    {
        if (left.Version != right.Version || left.Panels.Count != right.Panels.Count) return false;
        var leftByKind = left.Panels.ToDictionary(entry => entry.Kind);
        return right.Panels.All(entry =>
            leftByKind.TryGetValue(entry.Kind, out ToolboxPanelLayoutEntry? other) && entry.SameAs(other));
    }

    private static bool TryParseEntry(
        string segment, out ToolboxPanelKind kind, out ToolboxPanelLayoutEntry entry)
    {
        kind = default;
        entry = default!;
        string[] parts = segment.Split('=');
        if (parts.Length != 2) return false;
        if (!Enum.TryParse(parts[0], out ToolboxPanelKind parsedKind)) return false;
        string[] values = parts[1].Split(',');
        if (values.Length != 3) return false;
        if (values[0] != "0" && values[0] != "1") return false;
        bool visible = values[0] == "1";
        if (!int.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out int order) || order <= 0)
            return false;
        if (!float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float size) ||
            size < 0f || size > 1f)
            return false;
        kind = parsedKind;
        entry = new ToolboxPanelLayoutEntry(parsedKind, visible, order, size);
        return true;
    }
}
