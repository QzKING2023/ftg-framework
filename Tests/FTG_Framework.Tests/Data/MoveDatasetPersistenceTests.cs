#nullable enable
using System.Text;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class MoveDatasetPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ftg-moves-{Guid.NewGuid():N}");
    private const string Original = """
    {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":2,"recovery":3,"hit_advantage":4,"block_advantage":-1,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
    """;

    public MoveDatasetPersistenceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("")]
    [InlineData("../outside")]
    [InlineData("folder/file")]
    [InlineData("folder\\file")]
    [InlineData("C:\\outside")]
    public void Resolve_InvalidDocumentIdentifier_RejectsBeforeAccess(string identifier)
    {
        var service = new MoveDatasetPersistence(_root, CreateStore());
        Assert.Throws<ArgumentException>(() => service.Load(identifier));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public void Resolve_WindowsCaseAliasForSameDestination_IsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        WriteDocument("moves", Original);
        var service = new MoveDatasetPersistence(_root, CreateStore());
        service.Load("moves");

        Assert.Throws<ArgumentException>(() => service.Load("MOVES"));
    }

    [Fact]
    public void Save_StaleContent_ReturnsConflictWithoutMutation()
    {
        string path = WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store);
        var loaded = service.Load("moves");
        File.WriteAllText(path, Original.Replace("\"damage\":10", "\"damage\":12"), new UTF8Encoding(false));
        byte[] external = File.ReadAllBytes(path);

        var result = service.Save("moves", MoveAuthoringCandidate.FromDocument(loaded));

        Assert.Equal(MoveSaveStatus.Conflict, result.Status);
        Assert.Equal(loaded.ContentIdentity, result.ExpectedIdentity);
        Assert.NotEqual(result.ExpectedIdentity, result.CurrentIdentity);
        Assert.Equal(external, File.ReadAllBytes(path));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
    }

    [Fact]
    public void Save_PreCommitFault_PreservesBytesAndCommittedDataset()
    {
        string path = WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store, point =>
        {
            if (point == MovePersistenceFaultPoint.BeforeReplace) throw new IOException("injected");
        });
        var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });

        var result = service.Save("moves", candidate);

        Assert.Equal(MoveSaveStatus.Failed, result.Status);
        Assert.Equal(Encoding.UTF8.GetBytes(Original), File.ReadAllBytes(path));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData((int)MovePersistenceFaultPoint.AfterSerialize)]
    [InlineData((int)MovePersistenceFaultPoint.BeforeStageFlush)]
    [InlineData((int)MovePersistenceFaultPoint.AfterStage)]
    [InlineData((int)MovePersistenceFaultPoint.AfterStagedValidation)]
    [InlineData((int)MovePersistenceFaultPoint.BeforeContainmentRecheck)]
    [InlineData((int)MovePersistenceFaultPoint.BeforeReplace)]
    public void Save_EveryPreCommitFault_PreservesBytesAndDataset(int faultPointValue)
    {
        var faultPoint = (MovePersistenceFaultPoint)faultPointValue;
        string path = WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store, point =>
        {
            if (point == faultPoint) throw new IOException($"injected {point}");
        });
        var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });

        var result = service.Save("moves", candidate);

        Assert.Equal(MoveSaveStatus.Failed, result.Status);
        Assert.Equal(Encoding.UTF8.GetBytes(Original), File.ReadAllBytes(path));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void Save_ErrorCleanupFault_PreservesDestinationAndCommittedDataset()
    {
        string path = WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store, point =>
        {
            if (point is MovePersistenceFaultPoint.BeforeReplace or MovePersistenceFaultPoint.DuringErrorCleanup)
                throw new IOException($"injected {point}");
        });
        var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });

        var result = service.Save("moves", candidate);

        Assert.Equal(MoveSaveStatus.Failed, result.Status);
        Assert.Equal(Encoding.UTF8.GetBytes(Original), File.ReadAllBytes(path));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
        Assert.Single(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void Save_DestinationBecomesSymlinkBeforeReplacement_RejectsWithoutTouchingTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        string path = WriteDocument("moves", Original);
        string outside = Path.Combine(Path.GetTempPath(), $"ftg-outside-{Guid.NewGuid():N}.json");
        File.WriteAllText(outside, Original.Replace("\"damage\":10", "\"damage\":99"), new UTF8Encoding(false));
        byte[] outsideBytes = File.ReadAllBytes(outside);
        try
        {
            var store = CreateStore();
            var service = new MoveDatasetPersistence(_root, store, point =>
            {
                if (point != MovePersistenceFaultPoint.BeforeContainmentRecheck) return;
                File.Delete(path);
                File.CreateSymbolicLink(path, outside);
            });
            var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
                .EditMove("5A", move => move with { Damage = 11 });

            var result = service.Save("moves", candidate);

            Assert.Equal(MoveSaveStatus.Failed, result.Status);
            Assert.Equal(outsideBytes, File.ReadAllBytes(outside));
            Assert.Equal(10, store.GetMove("5A")!.Damage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            File.Delete(outside);
        }
    }

    [Fact]
    public void TryCommitMoveDataset_AfterFileCommit_PerformsOnlyPreparedReferenceSwap()
    {
        var store = CreateStore();
        MoveDefinition[] candidate = MoveDataLoader.LoadFromJson(Original);

        bool committed = store.TryCommitMoveDataset(candidate, store.MoveDatasetVersion, () =>
        {
            candidate[0] = null!;
            return true;
        });

        Assert.True(committed);
        Assert.NotNull(store.GetMove("5A"));
    }

    [Fact]
    public void Save_ValidCandidate_AtomicallyCommitsAndRuntimeReloads()
    {
        WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store);
        var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });

        var result = service.Save("moves", candidate);

        Assert.Equal(MoveSaveStatus.Succeeded, result.Status);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.Equal(11, Assert.Single(MoveDataLoader.LoadFromJson(File.ReadAllText(Path.Combine(_root, "moves.json")))).Damage);
    }

    [Fact]
    public void Save_PostCommitCleanupFault_RemainsSuccessfulAndEmitsDiagnostic()
    {
        WriteDocument("moves", Original);
        var diagnostics = new List<string>();
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store,
            point => { if (point == MovePersistenceFaultPoint.DuringPostCommitCleanup) throw new IOException("cleanup"); },
            diagnostics.Add);
        var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });

        var result = service.Save("moves", candidate);

        Assert.Equal(MoveSaveStatus.Succeeded, result.Status);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Save_CancelledBeforeCommit_PreservesPriorState()
    {
        string path = WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store);
        var candidate = MoveAuthoringCandidate.FromDocument(service.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = service.Save("moves", candidate, cancellation.Token);

        Assert.Equal(MoveSaveStatus.Failed, result.Status);
        Assert.Equal(Encoding.UTF8.GetBytes(Original), File.ReadAllBytes(path));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
    }

    [Fact]
    public void ViewModel_ConflictPreservesForm_ThenExplicitReapplyUsesCurrentIdentity()
    {
        string path = WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store);
        var vm = new MoveAuthoringViewModel(service.Load("moves"), service, "moves");
        vm.Edit("5A", move => move with { Damage = 11 });
        File.WriteAllText(path, Original.Replace("\"hit_advantage\":4", "\"hit_advantage\":5"), new UTF8Encoding(false));

        var conflict = vm.Save();
        Assert.Equal(MoveSaveStatus.Conflict, conflict.Status);
        Assert.Equal(11, vm.CurrentCandidate.Moves[0].Damage);

        vm.ReapplyAfterConflict(service.Load("moves"));
        Assert.True(vm.Validate().Success);
        Assert.Equal(MoveSaveStatus.Succeeded, vm.Save().Status);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.Equal(5, store.GetMove("5A")!.HitAdvantage);
    }

    [Fact]
    public async Task Save_TwoWritersFromSameVersion_ExactlyOneWins()
    {
        WriteDocument("moves", Original);
        var store = CreateStore();
        var service = new MoveDatasetPersistence(_root, store);
        var loaded = service.Load("moves");
        var first = MoveAuthoringCandidate.FromDocument(loaded).EditMove("5A", move => move with { Damage = 11 });
        var second = MoveAuthoringCandidate.FromDocument(loaded).EditMove("5A", move => move with { Damage = 12 });
        using var gate = new ManualResetEventSlim(false);

        Task<MoveSaveResult> save1 = Task.Run(() => { gate.Wait(); return service.Save("moves", first); });
        Task<MoveSaveResult> save2 = Task.Run(() => { gate.Wait(); return service.Save("moves", second); });
        gate.Set();
        MoveSaveResult[] results = await Task.WhenAll(save1, save2);

        Assert.Single(results, result => result.Status == MoveSaveStatus.Succeeded);
        Assert.Single(results, result => result.Status == MoveSaveStatus.Conflict);
        Assert.Contains(store.GetMove("5A")!.Damage, new[] { 11, 12 });
    }

    private DataStore CreateStore() => new(MoveDataLoader.LoadFromJson(Original), knockbackProfiles: new[]
    {
        new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
    });
    private string WriteDocument(string id, string content)
    {
        string path = Path.Combine(_root, $"{id}.json");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }
}
