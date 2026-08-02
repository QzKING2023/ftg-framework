#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests.DataLayer;

public sealed class RuntimeTuningServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ftg-tuning-{Guid.NewGuid():N}");
    private const string Moves = """
        {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":1,"recovery":1,"hit_advantage":1,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
        """;
    private const string Knockbacks = """
        {"schema_version":1,"knockback_profiles":[{"profile_id":"light","horizontal":1,"vertical":2,"gravity":3,"friction":4}]}
        """;
    private const string Responses = """
        {"schema_version":1,"physics_response_profiles":[{"profile_id":"default","knockback_multiplier":1,"gravity_scale":1,"friction":0.5,"air_friction":0.2,"participates_in_hitstop":true}]}
        """;

    public RuntimeTuningServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void CommitMove_UpdatesCanonicalJsonAndStoreOnce()
    {
        var (service, store) = CreateService();
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields) { ["damage"] = "11" };

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(
            selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, 1));

        Assert.True(result.Committed);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.Equal(1UL, store.MoveDatasetVersion);
        Assert.Equal(11, Assert.Single(MoveDataLoader.LoadFromJson(File.ReadAllText(Path.Combine(_root, "moves.json")))).Damage);
    }

    [Fact]
    public void CommitPhysics_UpdatesCanonicalJsonAndStoreOnce()
    {
        var (service, store) = CreateService();
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields) { ["horizontal"] = "9.5" };

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(
            selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, 1));

        Assert.True(result.Committed);
        Assert.Equal(9.5f, store.GetKnockbackProfile("light")!.Horizontal);
        Assert.Single(PhysicsDataLoader.LoadKnockbackProfilesFromJson(
            File.ReadAllText(Path.Combine(_root, "knockback.json"))));
    }

    [Fact]
    public void Commit_StaleIdentity_ReturnsExpectedAndCurrentConflict()
    {
        var (service, _) = CreateService();
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        RuntimeTuningBaseline baseline = service.Load(selection);
        File.WriteAllText(Path.Combine(_root, "moves.json"), Moves.Replace("\"damage\":10", "\"damage\":12"), new UTF8Encoding(false));

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(
            selection, baseline.Fields, baseline.ContentIdentity, baseline.DatasetVersion, 1));

        Assert.Equal(RuntimeTuningCommitStatus.Conflict, result.Status);
        Assert.Equal(baseline.ContentIdentity, result.ExpectedIdentity);
        Assert.NotEqual(result.ExpectedIdentity, result.CurrentIdentity);
    }

    [Fact]
    public void CommitMove_ExternalWriteConflictReloadAndReapplySucceeds()
    {
        var (service, store) = CreateService();
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        RuntimeTuningBaseline original = service.Load(selection);
        var candidate = new Dictionary<string, string>(original.Fields) { ["damage"] = "11" };
        File.WriteAllText(Path.Combine(_root, "moves.json"),
            Moves.Replace("\"damage\":10", "\"damage\":12"), new UTF8Encoding(false));

        RuntimeTuningCommitResult conflict = service.Commit(new RuntimeTuningCommitRequest(
            selection, candidate, original.ContentIdentity, original.DatasetVersion, 1));
        RuntimeTuningBaseline reloaded = service.Load(selection);
        RuntimeTuningBaseline duplicateReload = service.Load(selection);
        RuntimeTuningCommitResult reapplied = service.Commit(new RuntimeTuningCommitRequest(
            selection, candidate, duplicateReload.ContentIdentity, duplicateReload.DatasetVersion, 1));

        Assert.Equal(RuntimeTuningCommitStatus.Conflict, conflict.Status);
        Assert.Equal("12", reloaded.Fields["damage"]);
        Assert.NotEqual(original.ContentIdentity, reloaded.ContentIdentity);
        Assert.Equal(reloaded.ContentIdentity, duplicateReload.ContentIdentity);
        Assert.Equal(reloaded.DatasetVersion, duplicateReload.DatasetVersion);
        Assert.True(reapplied.Committed);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.Equal(2UL, store.MoveDatasetVersion);
        Assert.Equal(11, Assert.Single(MoveDataLoader.LoadFromJson(
            File.ReadAllText(Path.Combine(_root, "moves.json")))).Damage);
    }

    [Fact]
    public void LoadMove_ExternalInvalidReferencePreservesLastValidStore()
    {
        var (service, store) = CreateService();
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        RuntimeTuningBaseline original = service.Load(selection);
        File.WriteAllText(Path.Combine(_root, "moves.json"),
            Moves.Replace("\"knockback_profile_id\":\"light\"", "\"knockback_profile_id\":\"missing\""),
            new UTF8Encoding(false));

        MoveDatasetFormatException error = Assert.Throws<MoveDatasetFormatException>(() => service.Load(selection));

        Assert.Equal("moves[0].knockback_profile_id", error.FieldPath);
        Assert.Equal(10, store.GetMove("5A")!.Damage);
        Assert.Equal(original.ContentIdentity, store.CaptureMoveBaseline().ContentIdentity.Sha256);
        Assert.Equal(original.DatasetVersion, store.MoveDatasetVersion);
    }

    [Fact]
    public async Task Commit_TwoMoveWritersFromSameBaseline_ExactlyOneWins()
    {
        using var prepared = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(false);
        void AtBoundary(MovePersistenceFaultPoint point)
        {
            if (point != MovePersistenceFaultPoint.BeforeCoordinatedCommit) return;
            prepared.Signal();
            release.Wait();
        }
        var (service, store) = CreateService(moveFault: AtBoundary);
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        RuntimeTuningBaseline baseline = service.Load(selection);
        using var gate = new ManualResetEventSlim(false);
        RuntimeTuningCommitRequest Request(string damage) => new(selection,
            new Dictionary<string, string>(baseline.Fields) { ["damage"] = damage },
            baseline.ContentIdentity, baseline.DatasetVersion, 1);
        Task<RuntimeTuningCommitResult> first = Task.Run(() => { gate.Wait(); return service.Commit(Request("11")); });
        Task<RuntimeTuningCommitResult> second = Task.Run(() => { gate.Wait(); return service.Commit(Request("12")); });
        gate.Set();
        Assert.True(prepared.Wait(TimeSpan.FromSeconds(10)));
        release.Set();
        RuntimeTuningCommitResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Committed);
        Assert.Single(results, result => result.Status == RuntimeTuningCommitStatus.Conflict);
        Assert.Equal(1UL, store.MoveDatasetVersion);
    }

    [Fact]
    public async Task Commit_EditorAndRuntimeFromSameBaseline_ExactlyOneWins()
    {
        using var prepared = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(false);
        void AtBoundary(MovePersistenceFaultPoint point)
        {
            if (point != MovePersistenceFaultPoint.BeforeCoordinatedCommit) return;
            prepared.Signal();
            release.Wait();
        }
        var (runtime, store) = CreateService(moveFault: AtBoundary);
        var editor = new MoveDatasetPersistence(_root, store, AtBoundary);
        MoveDatasetDocument loaded = editor.Load("moves");
        MoveAuthoringCandidate editorCandidate = MoveAuthoringCandidate.FromDocument(loaded)
            .EditMove("5A", move => move with { Damage = 12 });
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        RuntimeTuningBaseline baseline = runtime.Load(selection);
        var request = new RuntimeTuningCommitRequest(selection,
            new Dictionary<string, string>(baseline.Fields) { ["damage"] = "11" },
            baseline.ContentIdentity, baseline.DatasetVersion, 1);
        using var gate = new ManualResetEventSlim(false);

        Task<MoveSaveResult> editorTask = Task.Run(() => { gate.Wait(); return editor.Save("moves", editorCandidate); });
        Task<RuntimeTuningCommitResult> runtimeTask = Task.Run(() => { gate.Wait(); return runtime.Commit(request); });
        gate.Set();
        Assert.True(prepared.Wait(TimeSpan.FromSeconds(10)));
        release.Set();
        await Task.WhenAll(editorTask, runtimeTask);
        MoveSaveResult editorResult = await editorTask;
        RuntimeTuningCommitResult runtimeResult = await runtimeTask;

        Assert.Equal(1, new[] { editorResult.Status == MoveSaveStatus.Succeeded, runtimeResult.Committed }.Count(value => value));
        Assert.Equal(1, new[] { editorResult.Status == MoveSaveStatus.Conflict, runtimeResult.Status == RuntimeTuningCommitStatus.Conflict }.Count(value => value));
        Assert.Equal(1UL, store.MoveDatasetVersion);
    }

    [Fact]
    public void CommitResponse_ColdRestartUsesOrdinarySchemaLoader()
    {
        var (service, _) = CreateService();
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.PhysicsResponseProfile, "response", "default");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields) { ["gravity_scale"] = "1.75" };

        Assert.True(service.Commit(new RuntimeTuningCommitRequest(selection, fields,
            baseline.ContentIdentity, baseline.DatasetVersion, 1)).Committed);

        PhysicsResponseProfile restarted = Assert.Single(PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(
            File.ReadAllText(Path.Combine(_root, "response.json"))));
        Assert.Equal(1.75f, restarted.GravityScale);
    }

    [Theory]
    [InlineData(RuntimeTuningFaultPoint.BeforeIdentityRead)]
    [InlineData(RuntimeTuningFaultPoint.BeforeSerialize)]
    [InlineData(RuntimeTuningFaultPoint.BeforeStageFlush)]
    [InlineData(RuntimeTuningFaultPoint.AfterStage)]
    [InlineData(RuntimeTuningFaultPoint.AfterStagedValidation)]
    [InlineData(RuntimeTuningFaultPoint.BeforeCommit)]
    [InlineData(RuntimeTuningFaultPoint.BeforeReplace)]
    public void CommitPhysics_PreCommitFaultPreservesBytesStoreAndVersion(RuntimeTuningFaultPoint faultPoint)
    {
        var (_, store) = CreateService();
        string path = Path.Combine(_root, "knockback.json");
        byte[] original = File.ReadAllBytes(path);
        ulong version = store.PhysicsDatasetVersion;
        var service = new RuntimeTuningService(_root, store, path, Path.Combine(_root, "response.json"),
            () => new[] { "default" }, point => { if (point == faultPoint) throw new IOException(point.ToString()); });
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields) { ["horizontal"] = "9" };

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(selection, fields,
            baseline.ContentIdentity, baseline.DatasetVersion, 1));

        Assert.False(result.Committed);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(1f, store.GetKnockbackProfile("light")!.Horizontal);
        Assert.Equal(version, store.PhysicsDatasetVersion);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void CommitPhysics_StagedBytesMutatedAfterValidation_RejectsWithoutPublishing()
    {
        var (_, store) = CreateService();
        string path = Path.Combine(_root, "knockback.json");
        byte[] original = File.ReadAllBytes(path);
        ulong version = store.PhysicsDatasetVersion;
        var service = new RuntimeTuningService(_root, store, path, Path.Combine(_root, "response.json"),
            () => new[] { "default" }, point =>
            {
                if (point != RuntimeTuningFaultPoint.BeforeReplace) return;
                string staged = Assert.Single(Directory.GetFiles(_root, "*.tmp"));
                File.WriteAllText(staged, Knockbacks.Replace("\"horizontal\":1", "\"horizontal\":77"),
                    new UTF8Encoding(false));
            });
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields) { ["horizontal"] = "9" };

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(selection, fields,
            baseline.ContentIdentity, baseline.DatasetVersion, 1));

        Assert.Equal(RuntimeTuningCommitStatus.IoFailure, result.Status);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(1f, store.GetKnockbackProfile("light")!.Horizontal);
        Assert.Equal(version, store.PhysicsDatasetVersion);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void CommitPhysics_PostCommitCleanupFaultReturnsDiagnosticSuccess()
    {
        var (_, store) = CreateService();
        string path = Path.Combine(_root, "knockback.json");
        var service = new RuntimeTuningService(_root, store, path, Path.Combine(_root, "response.json"),
            () => new[] { "default" }, point => { if (point == RuntimeTuningFaultPoint.DuringPostCommitCleanup) throw new IOException("cleanup"); });
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields) { ["horizontal"] = "9" };

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(selection, fields,
            baseline.ContentIdentity, baseline.DatasetVersion, 1));

        Assert.Equal(RuntimeTuningCommitStatus.CommittedWithDiagnostic, result.Status);
        Assert.Equal(9f, store.GetKnockbackProfile("light")!.Horizontal);
    }

    [Theory]
    [InlineData(RuntimeTuningSelectionKind.Move)]
    [InlineData(RuntimeTuningSelectionKind.KnockbackProfile)]
    public void Commit_InvalidatedAtFinalBoundary_CancelsWithoutMutation(RuntimeTuningSelectionKind kind)
    {
        var sessions = new RuntimeTuningSessionAuthority();
        ulong token = sessions.Capture();
        File.WriteAllText(Path.Combine(_root, "moves.json"), Moves, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(_root, "knockback.json"), Knockbacks, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(_root, "response.json"), Responses, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(Moves),
            knockbackProfiles: PhysicsDataLoader.LoadKnockbackProfilesFromJson(Knockbacks),
            physicsResponseProfiles: PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(Responses));
        string target = Path.Combine(_root, kind == RuntimeTuningSelectionKind.Move ? "moves.json" : "knockback.json");
        byte[] original = File.ReadAllBytes(target);
        var service = new RuntimeTuningService(_root, store,
            Path.Combine(_root, "knockback.json"), Path.Combine(_root, "response.json"),
            () => new[] { "default" },
            fault: point => { if (point == RuntimeTuningFaultPoint.BeforeReplace) sessions.Invalidate(); },
            isSessionCurrent: sessions.IsCurrent,
            moveFault: point => { if (point == MovePersistenceFaultPoint.BeforeReplace) sessions.Invalidate(); });
        var selection = kind == RuntimeTuningSelectionKind.Move
            ? new RuntimeTuningSelection(kind, "moves", "5A")
            : new RuntimeTuningSelection(kind, "knockback", "light");
        RuntimeTuningBaseline baseline = service.Load(selection);
        var fields = new Dictionary<string, string>(baseline.Fields)
        {
            [kind == RuntimeTuningSelectionKind.Move ? "damage" : "horizontal"] = "9"
        };
        ulong moveVersion = store.MoveDatasetVersion;
        ulong physicsVersion = store.PhysicsDatasetVersion;

        RuntimeTuningCommitResult result = service.Commit(new RuntimeTuningCommitRequest(
            selection, fields, baseline.ContentIdentity, baseline.DatasetVersion, token));

        Assert.Equal(RuntimeTuningCommitStatus.Cancelled, result.Status);
        Assert.Equal(original, File.ReadAllBytes(target));
        Assert.Equal(10, store.GetMove("5A")!.Damage);
        Assert.Equal(1f, store.GetKnockbackProfile("light")!.Horizontal);
        Assert.Equal(moveVersion, store.MoveDatasetVersion);
        Assert.Equal(physicsVersion, store.PhysicsDatasetVersion);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    private (RuntimeTuningService Service, DataStore Store) CreateService(
        Action<MovePersistenceFaultPoint>? moveFault = null)
    {
        File.WriteAllText(Path.Combine(_root, "moves.json"), Moves, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(_root, "knockback.json"), Knockbacks, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(_root, "response.json"), Responses, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(Moves),
            knockbackProfiles: PhysicsDataLoader.LoadKnockbackProfilesFromJson(Knockbacks),
            physicsResponseProfiles: PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(Responses));
        var service = new RuntimeTuningService(_root, store,
            Path.Combine(_root, "knockback.json"), Path.Combine(_root, "response.json"),
            () => new[] { "default" }, moveFault: moveFault);
        return (service, store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
