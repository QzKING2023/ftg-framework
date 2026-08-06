#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Editor;

public enum ToolboxPanelKind
{
    MoveAuthoring = 0,
    EventBusDebug = 1,
    RuntimeTuning = 2
}

public enum ToolboxSelectionIdentityKind
{
    Move = 0,
    KnockbackProfile = 1,
    PhysicsResponseProfile = 2
}

/// <summary>
/// Read-only cross-panel selection identity. Propagation never writes data and
/// never publishes EventBus outcomes (S4.3-AC02).
/// </summary>
public sealed record ToolboxSelection(
    ToolboxPanelKind Source,
    ToolboxSelectionIdentityKind IdentityKind,
    string ItemId)
{
    /// <summary>
    /// Applicability matrix (S4.3-AC02): a move identity applies to the runtime
    /// tuning panel. Knockback/physics identities have no consumer today because
    /// the only candidate consumer (move authoring) would have to write form
    /// state to accept them, which the read-only contract forbids.
    /// </summary>
    public bool IsApplicableTo(ToolboxPanelKind consumer) =>
        IdentityKind == ToolboxSelectionIdentityKind.Move &&
        consumer == ToolboxPanelKind.RuntimeTuning;
}

public sealed record ToolboxError(ToolboxPanelKind Source, string Operation, string Message);

/// <summary>
/// Adapter-supplied callbacks for one hosted panel. The workspace treats every
/// callback as non-fallible: no fallible operation may occur after the first
/// committed release (S4.3-AC13).
/// </summary>
public sealed class ToolboxPanelHandle
{
    public ToolboxPanelKind Kind { get; }
    public Action? OnSuspend { get; set; }
    public Action? OnResume { get; set; }
    public Action? OnShutdown { get; set; }
    public Action? OnRevalidate { get; set; }

    internal bool IsSuspended { get; set; }

    public ToolboxPanelHandle(ToolboxPanelKind kind)
    {
        Kind = kind;
    }
}

/// <summary>
/// Versioned workspace-layout value (S4.3-AC08). This is a UI preference, not a
/// data/save/replay container — it must never become a second path for
/// move/runtime data (gate item 5).
/// </summary>
public sealed record WorkspaceLayoutData(int Version, IReadOnlyList<ToolboxPanelLayoutEntry> Panels);

public sealed record ToolboxPanelLayoutEntry(
    ToolboxPanelKind Kind,
    bool Visible,
    int Order,
    float SizeProportion)
{
    public bool SameAs(ToolboxPanelLayoutEntry other) =>
        Kind == other.Kind && Visible == other.Visible && Order == other.Order &&
        Math.Abs(SizeProportion - other.SizeProportion) < 0.0001f;
}
