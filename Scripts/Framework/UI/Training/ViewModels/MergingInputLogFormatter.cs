#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.UI.Training.ViewModels;

// Merges runs of consecutive identical inputs (same player, type, value on
// adjacent frames) into a single row showing the frame range and repeat count.
public sealed class MergingInputLogFormatter : IInputLogFormatter
{
    public static readonly MergingInputLogFormatter Instance = new();

    private MergingInputLogFormatter() { }

    public IReadOnlyList<string> FormatRows(IReadOnlyList<InputLogViewModel.DisplayEntry> entries)
    {
        var rows = new List<string>(entries.Count);
        int i = 0;
        while (i < entries.Count)
        {
            var start = entries[i];
            int end = i;
            while (end + 1 < entries.Count && ContinuesRun(start, entries[end], entries[end + 1]))
                end++;

            int count = end - i + 1;
            rows.Add(count == 1
                ? InputLogViewModel.FormatEntry(start)
                : FormatRun(start, entries[end].Frame, count));
            i = end + 1;
        }
        return rows;
    }

    private static bool ContinuesRun(
        InputLogViewModel.DisplayEntry first,
        InputLogViewModel.DisplayEntry previous,
        InputLogViewModel.DisplayEntry next) =>
        next.PlayerId == first.PlayerId
        && next.Type == first.Type
        && next.Value == first.Value
        && next.Frame == previous.Frame + 1;

    private static string FormatRun(InputLogViewModel.DisplayEntry start, int endFrame, int count)
    {
        string typeChar = start.Type == InputType.Directional ? "D" : "B";
        string valueStr = InputLogViewModel.FormatValue(start.Type, start.Value);
        return $"{start.Frame,4}-{endFrame}  P{start.PlayerId}  {typeChar}  {valueStr} (×{count})";
    }
}
