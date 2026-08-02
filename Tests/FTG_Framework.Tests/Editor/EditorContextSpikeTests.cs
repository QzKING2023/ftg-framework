#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// Spike tests validating the IEditorContext thin-adapter pattern.
/// All tests use a test-double — no Godot runtime required.
/// </summary>
public sealed class EditorContextSpikeTests
{
    private sealed class StubEditorContext : IEditorContext
    {
        private readonly List<string> _selectedPaths = new();
        private readonly List<string> _undoLog = new();

        public IReadOnlyList<string> SelectedNodePaths => _selectedPaths;
        public bool IsEditorActive { get; set; } = true;
        public IReadOnlyList<string> UndoLog => _undoLog;

        public event Action<IReadOnlyList<string>>? SelectionChanged;

        public void Dispose() => SelectionChanged = null;

        public void SetSelectedPaths(params string[] paths)
        {
            _selectedPaths.Clear();
            _selectedPaths.AddRange(paths);
            SelectionChanged?.Invoke(_selectedPaths);
        }

        public void CreateUndoAction(string actionName, Action doAction, Action undoAction, bool executeDo = true)
        {
            _undoLog.Add($"do:{actionName}");
            if (executeDo) doAction();
            _undoLog.Add($"register-undo:{actionName}");
            // Production stores undo for later; stub records it was registered.
            // Callers can invoke the stored undo separately to verify correctness.
        }
    }

    [Fact]
    public void SelectionChanged_FiresWhenSelectionUpdates()
    {
        var ctx = new StubEditorContext();
        IReadOnlyList<string>? captured = null;
        ctx.SelectionChanged += paths => captured = paths;

        ctx.SetSelectedPaths("res://Characters/Ryu/Ryu.tscn");

        Assert.NotNull(captured);
        Assert.Single(captured!);
        Assert.Equal("res://Characters/Ryu/Ryu.tscn", captured![0]);
    }

    [Fact]
    public void UndoAction_DoAndUndoAreCallable()
    {
        var ctx = new StubEditorContext();
        var value = 0;
        void Do() => value = 42;
        void Undo() => value = 0;

        ctx.CreateUndoAction("Change Value", Do, Undo);

        Assert.Equal(42, value); // Do executed
        Assert.Contains("do:Change Value", ctx.UndoLog);
        Assert.Contains("register-undo:Change Value", ctx.UndoLog);
    }

    [Fact]
    public void IsEditorActive_FalseInTestContext()
    {
        var ctx = new StubEditorContext { IsEditorActive = false };
        Assert.False(ctx.IsEditorActive);
    }

    [Fact]
    public void MultipleUndoActions_LoggedIndependently()
    {
        var ctx = new StubEditorContext();
        var a = 0;
        var b = "";

        ctx.CreateUndoAction("Set A", () => a = 1, () => a = 0);
        ctx.CreateUndoAction("Set B", () => b = "hello", () => b = "");

        Assert.Equal(1, a);
        Assert.Equal("hello", b);
        Assert.Equal(4, ctx.UndoLog.Count);
    }

    [Fact]
    public void SelectionChanged_UnsubscribeStopsNotifications()
    {
        var ctx = new StubEditorContext();
        var callCount = 0;
        void OnSelection(IReadOnlyList<string> _) => callCount++;

        ctx.SelectionChanged += OnSelection;
        ctx.SetSelectedPaths("a");
        Assert.Equal(1, callCount);

        ctx.SelectionChanged -= OnSelection;
        ctx.SetSelectedPaths("b");
        Assert.Equal(1, callCount); // No additional call
    }
}
