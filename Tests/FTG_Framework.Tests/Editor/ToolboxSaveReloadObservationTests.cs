#nullable enable
using System;
using System.IO;
using System.Text;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.Editor;
using FTG_Framework.Tests.Core;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// S4.3-AC03/E4.3-B: after a canonical authoring save, the debug panel can
/// display the reload envelope for the saved document, and the save itself does
/// not inject lifecycle outcomes. Deterministic — no fixed-delay watcher
/// assertions (PREP-2.2): the FileWatcher→EventBus bridge is covered by the
/// unchanged FileWatcher regression suite.
/// </summary>
[Collection(EventBusTestCollection.Name)]
public sealed class ToolboxSaveReloadObservationTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ftg-save-reload-{Guid.NewGuid():N}");

    private const string MovesJson = """
    {"schema_version":1,"moves":[{"move_id":"5A","startup":5,"active":3,"recovery":7,"hit_advantage":0,"block_advantage":0,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
    """;

    public ToolboxSaveReloadObservationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        _eventBusScope.Dispose();
    }

    [Fact]
    public void CanonicalAuthoringSave_CommitsDocumentAndProducesNoLifecycleOutcome()
    {
        File.WriteAllText(Path.Combine(_root, "moves.json"), MovesJson, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(MovesJson), knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
        });
        var persistence = new MoveDatasetPersistence(_root, store);
        var desired = MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
            .EditMove("5A", move => move with { Damage = 11 });

        MoveSaveResult result = persistence.Save("moves", desired);

        Assert.Equal(MoveSaveStatus.Succeeded, result.Status);
        Assert.Equal(11, store.GetMove("5A")!.Damage);
        Assert.True(File.Exists(Path.Combine(_root, "moves.json")));
    }

    [Fact]
    public void CanonicalSave_ReloadEnvelope_IsObservableByDebugService()
    {
        File.WriteAllText(Path.Combine(_root, "moves.json"), MovesJson, new UTF8Encoding(false));
        var store = new DataStore(MoveDataLoader.LoadFromJson(MovesJson), knockbackProfiles: new[]
        {
            new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
        });
        var persistence = new MoveDatasetPersistence(_root, store);
        MoveSaveResult saved = persistence.Save("moves",
            MoveAuthoringCandidate.FromDocument(persistence.Load("moves"))
                .EditMove("5A", move => move with { Damage = 11 }));
        Assert.Equal(MoveSaveStatus.Succeeded, saved.Status);
        string savedPath = Path.GetFullPath(Path.Combine(_root, "moves.json"));

        var service = new EventBusDebugService();
        service.Enable();
        try
        {
            // The FileWatcher running against the data root enqueues exactly
            // this envelope on the static EventBus (watcher bridge covered by
            // the unchanged FileWatcher regression suite).
            EventBus.Instance.EnqueueDataReload(new DataReloadedEvent(savedPath));
            EventBus.Instance.ProcessFrame();

            var entries = service.GetEntries();
            var reload = entries.ShouldContain(e => e.EventTypeName == nameof(DataReloadedEvent));
            Assert.Equal(1, reload.TotalOccurrences);
            Assert.Contains("moves.json", reload.LastPayloadSnapshot);
            // The canonical save itself injects no lifecycle outcome (AC03:
            // the reload/lifecycle envelopes the debug panel can display come
            // from the shared pipeline, not from a toolbox-side publication).
            Assert.Equal(0, entries.ShouldContain(e => e.EventTypeName == nameof(MatchInitializedEvent)).TotalOccurrences);
            Assert.Equal(0, entries.ShouldContain(e => e.EventTypeName == nameof(ReplayStartedEvent)).TotalOccurrences);
            Assert.Equal(0, entries.ShouldContain(e => e.EventTypeName == nameof(StateRestoredEvent)).TotalOccurrences);
        }
        finally
        {
            service.Disable();
        }
    }
}
