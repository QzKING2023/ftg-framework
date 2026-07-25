#nullable enable
using System.Collections.Generic;

namespace FTG_Framework.UI.Training.ViewModels;

// Controls how raw input entries become display rows. The ViewModel handles
// storage, filtering, and scrolling; the formatter owns aggregation and text.
public interface IInputLogFormatter
{
    IReadOnlyList<string> FormatRows(IReadOnlyList<InputLogViewModel.DisplayEntry> entries);
}
