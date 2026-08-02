#if TOOLS
#nullable enable
using System;
using System.IO;
using FTG_Framework.Data;
using Godot;

namespace FTG_Framework.Editor;

[Tool]
public partial class FTGEditorPlugin : EditorPlugin
{
    private MoveAuthoringDock? _dock;
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
            var store = new DataStore(MoveDataLoader.LoadFromJson(moveJson),
                knockbackProfiles: PhysicsDataLoader.LoadKnockbackProfilesFromJson(physicsJson));
            var persistence = new MoveDatasetPersistence(dataRoot, store);
            var viewModel = new MoveAuthoringViewModel(persistence.Load("example_moves"), persistence, "example_moves");
            var undo = new MoveAuthoringUndoService(_context, persistence, "example_moves");
            _dock = new MoveAuthoringDock();
            _dock.Bind(viewModel, undo);
            _dockRegistrationAttempted = true;
            AddControlToDock(DockSlot.LeftBr, _dock);
            GD.Print("[FTG Editor] Move authoring dock registered.");
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
        MoveAuthoringDock? dock = _dock;
        _dock = null;
        bool removeDock = _dockRegistrationAttempted;
        _dockRegistrationAttempted = false;

        try
        {
            context?.Dispose();
        }
        catch (Exception ex)
        {
            GD.PushError($"[FTG Editor] Context cleanup failed: {ex.Message}");
        }

        if (dock is null || !IsInstanceValid(dock)) return;
        if (removeDock)
        {
            try
            {
                RemoveControlFromDocks(dock);
                GD.Print("[FTG Editor] Move authoring dock removed.");
            }
            catch (Exception ex)
            {
                GD.PushError($"[FTG Editor] Dock removal failed: {ex.Message}");
            }
        }
        try
        {
            dock.QueueFree();
        }
        catch (Exception ex)
        {
            GD.PushError($"[FTG Editor] Dock release failed: {ex.Message}");
        }
    }
}
#endif
