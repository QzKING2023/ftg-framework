#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.UI.Training.ViewModels;

// Default formatter: one row per entry, preserving the original frame-level display.
public sealed class FrameLevelInputLogFormatter : IInputLogFormatter
{
    public static readonly FrameLevelInputLogFormatter Instance = new();

    private FrameLevelInputLogFormatter() { }

    public IReadOnlyList<string> FormatRows(IReadOnlyList<InputLogViewModel.DisplayEntry> entries)
    {
        var rows = new List<string>(entries.Count);
        foreach (var entry in entries)
            rows.Add(InputLogViewModel.FormatEntry(entry));
        return rows;
    }
}
