#nullable enable
using System;
using System.IO;
using System.Text;
using FTG_Framework.Data;
using FTG_Framework.Editor;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// S4.3-AC04/AC05 (E4.3-F): both toolbox panels commit through the shared
/// optimistic persistence boundary — exactly one valid candidate commits and
/// the stale candidate receives the normal expected/current conflict result
/// (no last-writer-wins bypass). Errors at every service seam are scoped to
/// that operation; other panels remain usable and no panel reports success for
/// an operation that failed in another service.
/// </summary>
public sealed class ToolboxWriteConflictIsolationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ftg-write-conflict-{Guid.NewGuid():N}");

    private const string MovesJson = """
    {"schema_version":1,"moves":[{"move_id":"5A","startup":5,"active":3,"recovery":7,"hit_advantage":0,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
    """;

    public ToolboxWriteConflictIsolationTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "moves.json"), MovesJson, new UTF8Encoding(false));
        // The tuning service observes initial physics identities at construction.
        File.Copy(Path.Combine(FindRepoRoot(), "Scripts", "Framework", "Data",
            "example_knockback_profiles.json"), Path.Combine(_root, "example_knockback_profiles.json"));
        File.Copy(Path.Combine(FindRepoRoot(), "Scripts", "Framework", "Data",
            "example_physics_response_profiles.json"), Path.Combine(_root, "example_physics_response_profiles.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void SimultaneousMoveWriters_ExactlyOneCommits_StaleGetsNormalConflict()
    {
        // E2.2-C pattern extended to the toolbox-hosted panels: the tuning panel
        // commits through RuntimeTuningService while the authoring panel commits
        // through MoveDatasetPersistence — the same shared optimistic boundary
        // with content identities. Exactly one candidate commits; the stale
        // candidate receives the normal expected/current conflict result.
        var store = BuildStore();
        var persistence = new MoveDatasetPersistence(_root, store);
        var tuning = new RuntimeTuningService(_root, store,
            Path.Combine(_root, "example_knockback_profiles.json"),
            Path.Combine(_root, "example_physics_response_profiles.json"),
            requiredResponseIds: () => new[] { "default" });
        var sessions = new RuntimeTuningSessionAuthority();

        var tuningVm = new RuntimeTuningViewModel(tuning, sessions);
        tuningVm.Open(new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A"));
        tuningVm.EditField("damage", "12");

        var authoringVm = new MoveAuthoringViewModel(
            persistence.Load("moves"), persistence, "moves");
        authoringVm.Edit("5A", move => move with { Damage = 11 });

        RuntimeTuningCommitResult tuningResult = tuningVm.Apply();
        MoveSaveResult authoringResult = authoringVm.Save();

        Assert.Equal(RuntimeTuningCommitStatus.Succeeded, tuningResult.Status);
        Assert.Equal(MoveSaveStatus.Conflict, authoringResult.Status);
        Assert.NotNull(authoringResult.CurrentIdentity);
        Assert.NotEqual(authoringResult.ExpectedIdentity, authoringResult.CurrentIdentity);
        // No last-writer-wins: the first valid commit is the committed value.
        Assert.Equal(12, store.GetMove("5A")!.Damage);
    }

    [Fact]
    public void ReverseOrder_AuthoringFirst_TuningGetsNormalConflict()
    {
        var store = BuildStore();
        var persistence = new MoveDatasetPersistence(_root, store);
        var tuning = new RuntimeTuningService(_root, store,
            Path.Combine(_root, "example_knockback_profiles.json"),
            Path.Combine(_root, "example_physics_response_profiles.json"),
            requiredResponseIds: () => new[] { "default" });
        var sessions = new RuntimeTuningSessionAuthority();

        var tuningVm = new RuntimeTuningViewModel(tuning, sessions);
        tuningVm.Open(new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A"));
        tuningVm.EditField("damage", "12");

        var authoringVm = new MoveAuthoringViewModel(
            persistence.Load("moves"), persistence, "moves");
        authoringVm.Edit("5A", move => move with { Damage = 11 });

        MoveSaveResult authoringResult = authoringVm.Save();
        RuntimeTuningCommitResult tuningResult = tuningVm.Apply();

        Assert.Equal(MoveSaveStatus.Succeeded, authoringResult.Status);
        Assert.Equal(RuntimeTuningCommitStatus.Conflict, tuningResult.Status);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
    }

    [Fact]
    public void ToolboxAddsNoWritePath_AndNoSecondConcurrencyAuthority()
    {
        // AC04: the toolbox introduces no last-writer-wins bypass and no second
        // persistence/concurrency authority — it composes the existing shared
        // boundary only.
        string dock = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Scripts", "Framework", "Editor", "Toolbox", "ToolboxDock.cs"));
        string plugin = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Scripts", "Framework", "Editor", "FTGEditorPlugin.cs"));

        Assert.DoesNotContain(".Save(", dock);
        Assert.DoesNotContain(".Commit(", dock);
        Assert.DoesNotContain("CanonicalDestinationCoordinator", dock);
        Assert.DoesNotContain("CommittedDocumentRegistry", dock);
        Assert.DoesNotContain("MoveDatasetPersistence", dock);
        Assert.DoesNotContain(".Save(", plugin);
        Assert.DoesNotContain(".Commit(", plugin);
        Assert.DoesNotContain("CanonicalDestinationCoordinator", plugin);
        Assert.DoesNotContain("CommittedDocumentRegistry", plugin);
    }

    [Fact]
    public void FaultInjectedAuthoringSeam_ErrorScoped_OtherPanelRemainsUsable()
    {
        // E4.3-F: a failure at the authoring persistence seam is scoped to the
        // authoring operation; the tuning panel's state and error report are
        // untouched, and the shared boundary recovers once the fault clears.
        var store = BuildStore();
        bool failOnce = true;
        var persistence = new MoveDatasetPersistence(_root, store,
            fault: point =>
            {
                if (point == MovePersistenceFaultPoint.AfterSerialize && failOnce)
                {
                    failOnce = false;
                    throw new IOException("disk full");
                }
            });
        var vm = new MoveAuthoringViewModel(persistence.Load("moves"), persistence, "moves");
        vm.Edit("5A", move => move with { Damage = 11 });

        MoveSaveResult failed = vm.Save();

        Assert.Equal(MoveSaveStatus.Failed, failed.Status);
        Assert.Equal("disk full", failed.Diagnostic);

        var router = new ToolboxErrorRouter();
        router.ReportError(ToolboxPanelKind.MoveAuthoring, "save", $"Save failed: {failed.Diagnostic}");
        Assert.True(router.HasPendingErrors(ToolboxPanelKind.MoveAuthoring));
        Assert.False(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
        Assert.False(router.HasPendingErrors(ToolboxPanelKind.EventBusDebug));
        // The tuning panel's success report must not clear the authoring error
        // (no cross-panel false success — failures are sticky per source).
        router.ReportSuccess(ToolboxPanelKind.RuntimeTuning, "apply");
        Assert.True(router.HasPendingErrors(ToolboxPanelKind.MoveAuthoring));
        // The other panel remains usable: the same shared boundary commits
        // successfully once the fault clears.
        MoveSaveResult retry = vm.Save();
        Assert.Equal(MoveSaveStatus.Succeeded, retry.Status);
    }

    [Fact]
    public void FaultInjectedTuningSeam_TuningErrorScoped_AuthoringUnaffected()
    {
        var store = BuildStore();
        var tuning = new RuntimeTuningService(_root, store,
            Path.Combine(_root, "example_knockback_profiles.json"),
            Path.Combine(_root, "example_physics_response_profiles.json"),
            requiredResponseIds: () => new[] { "default" },
            fault: point =>
            {
                if (point == RuntimeTuningFaultPoint.BeforeSerialize)
                    throw new IOException("tuning write failed");
            });
        var sessions = new RuntimeTuningSessionAuthority();
        var vm = new RuntimeTuningViewModel(tuning, sessions);
        vm.Open(new RuntimeTuningSelection(RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light_hit"));
        vm.EditField("horizontal", "9.5");

        RuntimeTuningCommitResult result = vm.Apply();

        Assert.Equal(RuntimeTuningCommitStatus.IoFailure, result.Status);

        var router = new ToolboxErrorRouter();
        router.ReportError(ToolboxPanelKind.RuntimeTuning, "apply", $"Commit failed: {result.Diagnostic}");
        Assert.True(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
        Assert.False(router.HasPendingErrors(ToolboxPanelKind.MoveAuthoring));
        // Authoring's own later success report does not clear the tuning error.
        router.ReportSuccess(ToolboxPanelKind.MoveAuthoring, "save");
        Assert.True(router.HasPendingErrors(ToolboxPanelKind.RuntimeTuning));
        // The tuning panel itself remains usable: a committed baseline is still
        // loadable and the faulted commit left no partial write behind.
        var reloaded = tuning.Load(new RuntimeTuningSelection(
            RuntimeTuningSelectionKind.KnockbackProfile, "knockback", "light_hit"));
        Assert.Equal("light_hit", reloaded.Selection.ItemId);
    }

    private static DataStore BuildStore() => new(
        MoveDataLoader.LoadFromJson(MovesJson),
        knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 },
            // LoadKnockback resolves against the store's physics baseline, not
            // the on-disk example file the tuning service observes at ctor.
            new KnockbackProfile { ProfileId = "light_hit", Horizontal = 0.5f, Vertical = 0, Gravity = 0.5f, Friction = 0.3f }
        });

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root (project.godot) not found.");
    }
}
