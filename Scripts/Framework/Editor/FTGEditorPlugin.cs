#if TOOLS
#nullable enable
using System;
using System.IO;
using FTG_Framework.Data;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.Editor;

[Tool]
public partial class FTGEditorPlugin : EditorPlugin
{
    private ToolboxDock? _toolbox;
    private ToolboxPlaySessionBoundary? _boundary;
    private GodotEditorContext? _context;
    private bool _dockRegistrationAttempted;

    public override void _EnterTree()
    {
        try
        {
            _context = new GodotEditorContext(EditorInterface.Singleton, GetUndoRedo());
            string dataRoot = ResolveDataRoot();
            string moveJson = File.ReadAllText(Path.Combine(dataRoot, "example_moves.json"));
            string physicsJson = File.ReadAllText(Path.Combine(dataRoot, "example_knockback_profiles.json"));
            string responseJson = File.ReadAllText(Path.Combine(dataRoot, "example_physics_response_profiles.json"));
            var store = new DataStore(MoveDataLoader.LoadFromJson(moveJson),
                knockbackProfiles: PhysicsDataLoader.LoadKnockbackProfilesFromJson(physicsJson),
                physicsResponseProfiles: PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(responseJson));
            var persistence = new MoveDatasetPersistence(dataRoot, store);
            var viewModel = new MoveAuthoringViewModel(persistence.Load("example_moves"), persistence, "example_moves");
            var undo = new MoveAuthoringUndoService(_context, persistence, "example_moves");
            var authoring = new MoveAuthoringDock();
            authoring.Bind(viewModel, undo);

            var sessions = new RuntimeTuningSessionAuthority();
            var tuningService = new RuntimeTuningService(dataRoot, store,
                Path.Combine(dataRoot, "example_knockback_profiles.json"),
                Path.Combine(dataRoot, "example_physics_response_profiles.json"),
                requiredResponseIds: () => new[] { "default" });
            var tuning = new RuntimeTuningPanel
            {
                Service = tuningService,
                DataStore = store,
                Sessions = sessions,
                HostedMode = true
            };
            var debug = new EventBusDebugPanel { HostedMode = true };

            var workspace = new ToolboxWorkspaceService();
            var selection = new ToolboxSelectionService();
            var errors = new ToolboxErrorRouter();
            _toolbox = new ToolboxDock(workspace, selection, errors, dataRoot, authoring, debug, tuning);
            _dockRegistrationAttempted = true;
            AddControlToDock(DockSlot.LeftBr, _toolbox);
            _boundary = new ToolboxPlaySessionBoundary(workspace);
            AddDebuggerPlugin(_boundary);
            _toolbox.Open();
            GD.Print("[FTG Editor] Unified toolbox dock registered.");
        }
        catch { Cleanup(); throw; }
    }

    public override void _ExitTree() => Cleanup();

    private static string ResolveDataRoot()
    {
        string[] candidates =
        {
            ProjectSettings.GlobalizePath("res://Scripts/Framework/Data"),
            Path.Combine(ProjectSettings.GlobalizePath("res://addons/ftg-framework"), "src", "Data"),
        };
        foreach (string candidate in candidates)
            if (File.Exists(Path.Combine(candidate, "example_moves.json")) &&
                File.Exists(Path.Combine(candidate, "example_knockback_profiles.json")))
                return candidate;
        throw new DirectoryNotFoundException(
            "[Editor] FTG move data was not found in the repository or installed-addon layout.");
    }

    private void Cleanup()
    {
        GodotEditorContext? context = _context;
        _context = null;
        ToolboxDock? toolbox = _toolbox;
        _toolbox = null;
        ToolboxPlaySessionBoundary? boundary = _boundary;
        _boundary = null;
        bool removeDock = _dockRegistrationAttempted;
        _dockRegistrationAttempted = false;

        // All fallible operations run before the toolbox's first committed
        // release (S4.3-AC13); the workspace close happens inside the dock's
        // _ExitTree during RemoveControlFromDocks.
        try
        {
            context?.Dispose();
        }
        catch (Exception ex)
        {
            GD.PushError($"[FTG Editor] Context cleanup failed: {ex.Message}");
        }

        if (boundary is not null && IsInstanceValid(boundary))
        {
            try
            {
                RemoveDebuggerPlugin(boundary);
            }
            catch (Exception ex)
            {
                GD.PushError($"[FTG Editor] Debugger plugin removal failed: {ex.Message}");
            }
        }

        if (toolbox is null || !IsInstanceValid(toolbox)) return;
        if (removeDock)
        {
            try
            {
                RemoveControlFromDocks(toolbox);
                GD.Print("[FTG Editor] Unified toolbox dock removed.");
            }
            catch (Exception ex)
            {
                GD.PushError($"[FTG Editor] Dock removal failed: {ex.Message}");
            }
        }
        try
        {
            toolbox.QueueFree();
        }
        catch (Exception ex)
        {
            GD.PushError($"[FTG Editor] Dock release failed: {ex.Message}");
        }
    }
}
#endif
