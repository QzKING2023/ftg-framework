#nullable enable
using System.IO;
using Xunit;

namespace FTG_Framework.Tests.Editor;

/// <summary>
/// E4.3-D/AC06/AC10 source-level parity: toolbox-hosted panels keep their
/// standalone behavior (hosting affordances are additive), the dock owns no
/// panel logic (delegation only), and Godot singleton access stays confined to
/// the thin adapters.
/// </summary>
public sealed class ToolboxSourceParityTests
{
    private static string ReadSource(params string[] relativePath)
    {
        string path = Path.Combine(FindRepoRoot(), Path.Combine(relativePath));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ToolboxDock_DelegatesPanelLogic_WithoutDuplicatingServices()
    {
        string dock = ReadSource("Scripts", "Framework", "Editor", "Toolbox", "ToolboxDock.cs");

        // AC02 wiring: authoring selection flows through the shared coordination service.
        Assert.Contains("_authoring.SelectionChanged +=", dock);
        Assert.Contains("_selection.PropagateSelection(new ToolboxSelection(", dock);
        // AC09 wiring: lifecycle revalidation reaches the tuning panel's existing seam.
        Assert.Contains("_tuning.ReconstructForLifecycle(\"lifecycle boundary\")", dock);
        Assert.Contains("_workspace.BoundaryTraced += message => GD.Print(message);", dock);
        // AC06 wiring: hidden panels suspend; re-shown panels resume from authoritative data.
        Assert.Contains("_tuning.SetSuspended(true)", dock);
        Assert.Contains("_tuning.SetSuspended(false)", dock);
        Assert.Contains("_debug.SetActive(true)", dock);
        Assert.Contains("_debug.SetActive(false)", dock);
        // AC10: Godot singleton access confined to the thin adapter.
        Assert.Contains("EditorInterface.Singleton.GetEditorSettings()", dock);
        // E4.3-D: the dock holds ready-made panels and never re-implements their
        // ViewModels, services, persistence, or validation.
        Assert.DoesNotContain("MoveAuthoringViewModel", dock);
        Assert.DoesNotContain("RuntimeTuningViewModel", dock);
        Assert.DoesNotContain("EventBusDebugViewModel", dock);
        Assert.DoesNotContain("MoveDatasetPersistence", dock);
        Assert.DoesNotContain("RuntimeTuningService", dock);
        Assert.DoesNotContain("EventBusDebugService", dock);
        Assert.DoesNotContain("MoveDatasetCodec", dock);
    }

    [Fact]
    public void EditorPlugin_ComposesExistingServices_WithoutNewLogic()
    {
        string plugin = ReadSource("Scripts", "Framework", "Editor", "FTGEditorPlugin.cs");

        // AC01: the plugin binds the existing pure-C# services/ViewModels; no
        // duplicated validation, persistence, tracing, or tuning logic.
        Assert.Contains("new MoveDatasetPersistence(dataRoot, store)", plugin);
        Assert.Contains("new MoveAuthoringUndoService(_context, persistence, \"example_moves\")", plugin);
        Assert.Contains("new RuntimeTuningService(dataRoot, store,", plugin);
        Assert.Contains("authoring.Bind(viewModel, undo)", plugin);
        Assert.Contains("HostedMode = true", plugin);
        Assert.Contains("AddControlToDock(DockSlot.LeftBr, _toolbox)", plugin);
        Assert.Contains("AddDebuggerPlugin(_boundary)", plugin);
        Assert.Contains("RemoveControlFromDocks(toolbox)", plugin);
        Assert.DoesNotContain("MoveDatasetCodec", plugin);
        Assert.DoesNotContain("JsonSerializer", plugin);
    }

    [Fact]
    public void RuntimeTuningPanel_HostingAffordancesAreAdditive()
    {
        string source = ReadSource("Scripts", "Framework", "UI", "Training", "RuntimeTuningPanel.cs");

        Assert.Contains("public bool HostedMode { get; set; }", source);
        Assert.Contains("if (!HostedMode)", source);
        Assert.Contains("EnsureControllerActions();", source);
        Assert.Contains("if (HostedMode) return; // hosted-toolbox affordance: no runtime keyboard shortcuts", source);
        Assert.Contains("internal void SetSuspended(bool suspended)", source);
        Assert.Contains("internal void SelectMoveItem(string moveId)", source);
        Assert.Contains("internal void ReconstructForLifecycle(string reason)", source);
        Assert.Contains("internal Action<string>? ErrorReported { get; set; }", source);
    }

    [Fact]
    public void EventBusDebugPanel_HostingAffordancesAreAdditive()
    {
        string source = ReadSource("Scripts", "Framework", "UI", "Training", "EventBusDebugPanel.cs");

        Assert.Contains("internal bool HostedMode { get; set; }", source);
        Assert.Contains("internal void SetActive(bool active)", source);
        Assert.Contains("if (HostedMode && !Visible)", source);
        // Standalone close toggle stays; hosted mode omits it.
        Assert.Contains("if (!HostedMode)", source);
    }

    [Fact]
    public void ToolboxDock_LayoutPersistence_IsConfinedToVersionedEditorSettingsBoundary()
    {
        // E4.3-E: the thin adapter stores/loads the versioned layout value
        // exclusively through EditorSettings — no file persistence, no data
        // container, no second concurrency authority for a UI preference.
        string dock = ReadSource("Scripts", "Framework", "Editor", "Toolbox", "ToolboxDock.cs");

        Assert.Contains("_settings.SetSetting(ToolboxLayoutService.EditorSettingsKey,", dock);
        Assert.Contains("_settings.HasSetting(ToolboxLayoutService.EditorSettingsKey)", dock);
        Assert.Contains("_settings.GetSetting(ToolboxLayoutService.EditorSettingsKey).AsString()", dock);
        Assert.Contains("return ToolboxLayoutService.Parse(raw);", dock);
        Assert.Contains("return ToolboxLayoutService.DefaultLayout();", dock);
        Assert.DoesNotContain("File.", dock);
        Assert.DoesNotContain("CanonicalDestinationCoordinator", dock);
        Assert.DoesNotContain("CommittedDocumentRegistry", dock);
    }

    [Fact]
    public void MoveAuthoringDock_ExposesSelectionAndErrorHooks()
    {
        string source = ReadSource("Scripts", "Framework", "Editor", "MoveAuthoringDock.cs");

        Assert.Contains("internal string? SelectedMoveId => _moveId;", source);
        Assert.Contains("internal Action<string?>? SelectionChanged { get; set; }", source);
        Assert.Contains("internal Action<string>? ErrorReported { get; set; }", source);
        Assert.Contains("ReportSelection()", source);
    }

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
