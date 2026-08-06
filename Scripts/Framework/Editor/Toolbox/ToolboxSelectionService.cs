#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Editor;

/// <summary>
/// Read-only selection coordination (S4.3-AC02). The service propagates a
/// selected move/profile identity to applicable panels only; propagation never
/// writes data and never injects EventBus outcomes (the service holds no
/// reference to EventBus or Data types).
/// </summary>
public sealed class ToolboxSelectionService
{
    private readonly List<SelectionSubscriber> _subscribers = new();

    public ToolboxSelection? CurrentSelection { get; private set; }
    public event Action<ToolboxSelection>? SelectionChanged;

    public void Subscribe(ToolboxPanelKind consumer, Action<ToolboxSelection> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _subscribers.Add(new SelectionSubscriber(consumer, handler));
    }

    public void Unsubscribe(ToolboxPanelKind consumer, Action<ToolboxSelection> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _subscribers.RemoveAll(item => item.Consumer == consumer && item.Handler == handler);
    }

    /// <summary>
    /// Propagates a selection to applicable subscribers. Identical repeats are
    /// deduplicated (no redundant panel refreshes). Returns whether any
    /// subscriber received the selection.
    /// </summary>
    public bool PropagateSelection(ToolboxSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection == CurrentSelection)
            return false;

        bool delivered = false;
        foreach (SelectionSubscriber subscriber in _subscribers.ToArray())
            if (selection.IsApplicableTo(subscriber.Consumer))
            {
                subscriber.Handler(selection);
                delivered = true;
            }
        CurrentSelection = selection;
        SelectionChanged?.Invoke(selection);
        return delivered;
    }

    private sealed record SelectionSubscriber(
        ToolboxPanelKind Consumer,
        Action<ToolboxSelection> Handler);
}
