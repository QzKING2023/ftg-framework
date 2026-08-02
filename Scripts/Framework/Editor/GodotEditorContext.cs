#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace FTG_Framework.Editor;

public sealed partial class GodotEditorContext : IEditorContext
{
    private readonly EditorSelection _editorSelection;
    private readonly EditorUndoRedoManager _undoRedo;
    private readonly Action _selectionHandler;
    private bool _disposed;

    public bool IsEditorActive => !_disposed;

    public IReadOnlyList<string> SelectedNodePaths
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var paths = new List<string>();
            foreach (var node in _editorSelection.GetSelectedNodes())
                if (node is Node godotNode)
                    paths.Add(godotNode.GetPath().ToString());
            return paths;
        }
    }

    public event Action<IReadOnlyList<string>>? SelectionChanged;

    public GodotEditorContext(EditorInterface editorInterface, EditorUndoRedoManager undoRedo)
    {
        ArgumentNullException.ThrowIfNull(editorInterface);
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
        _editorSelection = editorInterface.GetSelection();
        _selectionHandler = OnSelectionChanged;
        _editorSelection.SelectionChanged += _selectionHandler;
    }

    public void CreateUndoAction(string actionName, Action doAction, Action undoAction, bool executeDo = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ArgumentNullException.ThrowIfNull(doAction);
        ArgumentNullException.ThrowIfNull(undoAction);
        _undoRedo.CreateAction(actionName);
        var undoRelay = new EditorUndoRelay(doAction, undoAction, () => !_disposed);
        _undoRedo.AddDoMethod(undoRelay, EditorUndoRelay.MethodName.InvokeDo);
        _undoRedo.AddUndoMethod(undoRelay, EditorUndoRelay.MethodName.InvokeUndo);
        _undoRedo.CommitAction(executeDo);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editorSelection.SelectionChanged -= _selectionHandler;
        SelectionChanged = null;
    }

    private void OnSelectionChanged()
    {
        if (!_disposed)
            SelectionChanged?.Invoke(SelectedNodePaths);
    }

}

internal sealed partial class EditorUndoRelay : RefCounted
{
    private readonly Action _do;
    private readonly Action _undo;
    private readonly Func<bool> _active;

    internal EditorUndoRelay(Action doAction, Action undoAction, Func<bool> active) =>
        (_do, _undo, _active) = (doAction, undoAction, active);

    public void InvokeDo() { if (_active()) _do(); }
    public void InvokeUndo() { if (_active()) _undo(); }
}
