#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Editor;

/// <summary>
/// Thin abstraction over Godot EditorPlugin APIs (EditorInterface, EditorSelection, UndoRedo).
/// Enables pure C# testing of editor tooling logic without Godot runtime dependencies.
/// </summary>
public interface IEditorContext
{
    /// <summary>
    /// Currently selected node paths in the scene tree.
    /// </summary>
    IReadOnlyList<string> SelectedNodePaths { get; }

    /// <summary>
    /// Fired when the editor selection changes.
    /// </summary>
    event Action<IReadOnlyList<string>>? SelectionChanged;

    /// <summary>
    /// Register an undo/redo action. The <paramref name="undo"/> callback reverses the
    /// effect of <paramref name="doAction"/>. Both are invoked on the main thread.
    /// </summary>
    void CreateUndoAction(string actionName, Action doAction, Action undoAction);

    /// <summary>
    /// True when running inside the Godot editor (EditorPlugin context).
    /// False during unit testing — allows test-safe code paths.
    /// </summary>
    bool IsEditorActive { get; }
}
