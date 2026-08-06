#if TOOLS
#nullable enable
using Godot;

namespace FTG_Framework.Editor;

/// <summary>
/// Editor play-session boundary (S4.3-AC09). Godot 4.5's documented mechanism
/// is EditorDebuggerPlugin._SetupSession + EditorDebuggerSession started/stopped
/// signals (verified against the pinned 4.5.1 GodotSharpEditor API); the
/// stopped signal is the only editor-side notification that a play session
/// ended, because the game runs in a separate process and publishes no
/// lifecycle event on the editor-process EventBus singleton. The workspace
/// treats session end as a lifecycle boundary and revalidates panels.
/// </summary>
[Tool]
public partial class ToolboxPlaySessionBoundary : EditorDebuggerPlugin
{
    private readonly ToolboxWorkspaceService _workspace;

    public ToolboxPlaySessionBoundary(ToolboxWorkspaceService workspace)
    {
        _workspace = workspace ?? throw new System.ArgumentNullException(nameof(workspace));
    }

    public override void _SetupSession(int sessionId)
    {
        EditorDebuggerSession? session = GetSession(sessionId);
        if (session is null)
            return;
        // Disconnect-then-connect keeps the wiring idempotent across session
        // re-setup for the same id.
        session.Started -= OnSessionStarted;
        session.Stopped -= OnSessionStopped;
        session.Started += OnSessionStarted;
        session.Stopped += OnSessionStopped;
    }

    private void OnSessionStarted() => _workspace.OnPlaySessionStarted();

    private void OnSessionStopped() => _workspace.OnPlaySessionEnded();
}
#endif
