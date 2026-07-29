#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace FTG_Framework.Editor;

/// <summary>
/// Production adapter wrapping Godot EditorPlugin APIs.
/// All Godot dependencies are confined to this class — the rest of the editor tooling
/// logic works against <see cref="IEditorContext"/> and is pure-C# testable.
///
/// SPIKE NOTE (2026-07-29): The UndoRedo API signature in Godot 4.5.1 differs from
/// earlier 4.x versions. The CreateUndoAction implementation requires in-editor
/// verification of the exact EditorUndoRedoManager method signatures. The thin-adapter
/// pattern is validated (see EditorContextSpikeTests) — the Godot type wiring is a
/// mechanical step to be completed when the editor tooling is implemented in Epic 4.
/// </summary>
public sealed class GodotEditorContext : IEditorContext
{
    private readonly EditorInterface _editorInterface;
    private readonly EditorSelection _editorSelection;

    public bool IsEditorActive => true;

    public IReadOnlyList<string> SelectedNodePaths
    {
        get
        {
            var paths = new List<string>();
            foreach (var node in _editorSelection.GetSelectedNodes())
            {
                if (node is Node godotNode)
                    paths.Add(godotNode.GetPath().ToString());
            }
            return paths;
        }
    }

    public event Action<IReadOnlyList<string>>? SelectionChanged;

    public GodotEditorContext(EditorInterface editorInterface)
    {
        _editorInterface = editorInterface;
        _editorSelection = editorInterface.GetSelection();

        _editorSelection.SelectionChanged += () =>
        {
            SelectionChanged?.Invoke(SelectedNodePaths);
        };
    }

    // EditorUndoRedoManager API in Godot 4.5.1 requires in-editor verification.
    // The IEditorContext abstraction isolates this dependency — all consuming code
    // tests against the interface, not this implementation.
    public void CreateUndoAction(string actionName, Action doAction, Action undoAction)
    {
        var undoRedo = _editorInterface.GetEditorUndoRedo();
        if (undoRedo is null) return;

        // Godot 4.5 EditorUndoRedoManager.CreateAction signature TBD in-editor.
        undoRedo.CreateAction(actionName);
        doAction();
        // undoRedo.AddDoMethod(...) — exact params verified in Godot editor.
        // undoRedo.AddUndoMethod(...) — exact params verified in Godot editor.
        undoRedo.CommitAction();
    }
}
