#if TOOLS
#nullable enable
using System.IO;
using FTG_Framework.Data;
using Godot;

namespace FTG_Framework.Editor;

[Tool]
public partial class FTGEditorPlugin : EditorPlugin
{
    private MoveAuthoringDock? _dock;
    private GodotEditorContext? _context;

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
            AddControlToDock(DockSlot.LeftBr, _dock);
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
        _context?.Dispose();
        _context = null;
        if (_dock is null) return;
        if (IsInstanceValid(_dock)) { RemoveControlFromDocks(_dock); _dock.QueueFree(); }
        _dock = null;
    }
}
#endif
