#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Editor;

/// <summary>
/// Per-operation error scoping (S4.3-AC05). An error in one panel is scoped to
/// that operation; other panels remain usable, and no panel reports success for
/// an operation that failed in another service. Failures are sticky per source:
/// a later success report does not clear an outstanding error — only
/// <see cref="Clear"/> does — so a recovered source cannot mask a still-broken one.
/// </summary>
public sealed class ToolboxErrorRouter
{
    private readonly Dictionary<ToolboxPanelKind, ToolboxError?> _lastBySource = new();

    public event Action<ToolboxError>? ErrorReported;

    public ToolboxError? LastError(ToolboxPanelKind source) =>
        _lastBySource.TryGetValue(source, out ToolboxError? error) ? error : null;

    public bool HasPendingErrors(ToolboxPanelKind source) => LastError(source) is not null;

    public void ReportError(ToolboxPanelKind source, string operation, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var error = new ToolboxError(source, operation, message);
        _lastBySource[source] = error;
        ErrorReported?.Invoke(error);
    }

    public void ReportSuccess(ToolboxPanelKind source, string operation)
    {
        // Does not clear a sticky failure from the same source; the workspace
        // status remains truthful until the source is explicitly cleared.
    }

    public void Clear(ToolboxPanelKind source) => _lastBySource.Remove(source);

    public void ClearAll() => _lastBySource.Clear();
}
