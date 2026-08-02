#nullable enable
using System.Text;
using FTG_Framework.Data;
using FTG_Framework.Editor;
using Xunit;

namespace FTG_Framework.Tests.Editor;

public sealed class MoveAuthoringUndoServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ftg-undo-{Guid.NewGuid():N}");
    private const string Json = """
    {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":0,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
    """;

    public MoveAuthoringUndoServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void SaveUndoRedo_AllUseExpectedVersionAtomicTransaction()
    {
        File.WriteAllText(Path.Combine(_root, "moves.json"), Json, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(Json), knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
        });
        var persistence = new MoveDatasetPersistence(_root, store);
        var desired = MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });
        var context = new UndoContext();
        var service = new MoveAuthoringUndoService(context, persistence, "moves");

        service.SaveUndoable(desired);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        context.Undo();
        Assert.Equal(10, store.GetMove("5A")!.Damage);
        context.Redo();
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.Equal(MoveSaveStatus.Succeeded, service.LastResult!.Status);
    }

    [Fact]
    public void SaveUndoable_WhenEditorDoesNotInvokeRegisteredDo_CompletesCurrentSaveSynchronously()
    {
        File.WriteAllText(Path.Combine(_root, "moves.json"), Json, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(Json), knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
        });
        var persistence = new MoveDatasetPersistence(_root, store);
        var context = new ConditionalUndoContext();
        var service = new MoveAuthoringUndoService(context, persistence, "moves");
        var first = MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });
        service.SaveUndoable(first);
        Assert.Equal(MoveSaveStatus.Succeeded, service.LastResult!.Status);

        context.ExecuteDo = false;
        var second = MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
            .EditMove("5A", move => move with { Damage = 12 });
        service.SaveUndoable(second);

        Assert.Equal(MoveSaveStatus.Succeeded, service.LastResult!.Status);
        Assert.Equal(12, store.GetMove("5A")!.Damage);
    }

    [Fact]
    public void SaveUndoable_WhenFileChangedAfterCandidateLoad_ReportsConflictWithoutOverwrite()
    {
        string path = Path.Combine(_root, "moves.json");
        File.WriteAllText(path, Json, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(Json), knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
        });
        var persistence = new MoveDatasetPersistence(_root, store);
        var desired = MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });
        byte[] externalBytes = new UTF8Encoding(false).GetBytes(Json + Environment.NewLine);
        File.WriteAllBytes(path, externalBytes);
        var service = new MoveAuthoringUndoService(new UndoContext(), persistence, "moves");

        service.SaveUndoable(desired);

        Assert.Equal(MoveSaveStatus.Conflict, service.LastResult!.Status);
        Assert.Equal(externalBytes, File.ReadAllBytes(path));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
    }

    [Fact]
    public void SaveUndoable_WhenEditorDefersDoCallback_CompletesCurrentSaveBeforeReturning()
    {
        File.WriteAllText(Path.Combine(_root, "moves.json"), Json, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(Json), knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
        });
        var persistence = new MoveDatasetPersistence(_root, store);
        var desired = MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });
        var context = new DeferredUndoContext();
        var service = new MoveAuthoringUndoService(context, persistence, "moves");

        service.SaveUndoable(desired);

        Assert.Equal(MoveSaveStatus.Succeeded, service.LastResult!.Status);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.False(context.HasPendingInitialDo);
    }

    private sealed class UndoContext : IEditorContext
    {
        private Action? _do;
        private Action? _undo;
        public IReadOnlyList<string> SelectedNodePaths => Array.Empty<string>();
        public bool IsEditorActive => true;
        public event Action<IReadOnlyList<string>>? SelectionChanged { add { } remove { } }
        public void CreateUndoAction(string actionName, Action doAction, Action undoAction, bool executeDo = true)
        { _do = doAction; _undo = undoAction; if (executeDo) _do(); }
        public void Undo() => _undo!();
        public void Redo() => _do!();
        public void Dispose() { }
    }

    private sealed class ConditionalUndoContext : IEditorContext
    {
        public bool ExecuteDo { get; set; } = true;
        public IReadOnlyList<string> SelectedNodePaths => Array.Empty<string>();
        public bool IsEditorActive => true;
        public event Action<IReadOnlyList<string>>? SelectionChanged { add { } remove { } }
        public void CreateUndoAction(string actionName, Action doAction, Action undoAction, bool executeDo = true)
        {
            if (executeDo && ExecuteDo) doAction();
        }
        public void Dispose() { }
    }

    private sealed class DeferredUndoContext : IEditorContext
    {
        public bool HasPendingInitialDo { get; private set; }
        public IReadOnlyList<string> SelectedNodePaths => Array.Empty<string>();
        public bool IsEditorActive => true;
        public event Action<IReadOnlyList<string>>? SelectionChanged { add { } remove { } }
        public void CreateUndoAction(string actionName, Action doAction, Action undoAction, bool executeDo = true) =>
            HasPendingInitialDo = executeDo;
        public void Dispose() { }
    }
}
