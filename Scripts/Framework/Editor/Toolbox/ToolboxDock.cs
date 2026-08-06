#if TOOLS
#nullable enable
using System;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.Editor;

/// <summary>
/// Thin Godot adapter for the unified toolbox (S4.3-AC01/AC08/AC10). Hosts the
/// Story 2.1 move-authoring dock, Story 3.4 EventBus debug panel, and Story 2.2
/// runtime-tuning panel; all orchestration (layout, visibility, selection,
/// error routing, panel lifecycle) lives in pure-C# services. Godot singleton
/// access (EditorSettings) stays confined to this adapter. Panel ordering
/// within the dock is governed by the versioned layout value; the dock itself
/// is positioned by Godot editor docking (epic preamble).
/// </summary>
[Tool]
public partial class ToolboxDock : VBoxContainer
{
    private static readonly ToolboxPanelKind[] AllKinds =
    {
        ToolboxPanelKind.MoveAuthoring,
        ToolboxPanelKind.EventBusDebug,
        ToolboxPanelKind.RuntimeTuning
    };

    private readonly ToolboxWorkspaceService _workspace;
    private readonly ToolboxSelectionService _selection;
    private readonly ToolboxErrorRouter _errors;
    private readonly string _dataRoot;

    private readonly MoveAuthoringDock _authoring;
    private readonly EventBusDebugPanel _debug;
    private readonly RuntimeTuningPanel _tuning;
    private readonly HSplitContainer _outerSplit = new();
    private readonly HSplitContainer _innerSplit = new();
    private readonly Label _statusLabel = new();
    private readonly EditorSettings _settings;
    private readonly ToolboxPanelHandle _authoringHandle;
    private readonly ToolboxPanelHandle _debugHandle;
    private readonly ToolboxPanelHandle _tuningHandle;
    private WorkspaceLayoutData _layout;
    private FileWatcher? _watcher;
    private bool _shutdown;

    public ToolboxDock(
        ToolboxWorkspaceService workspace,
        ToolboxSelectionService selection,
        ToolboxErrorRouter errors,
        string dataRoot,
        MoveAuthoringDock authoring,
        EventBusDebugPanel debug,
        RuntimeTuningPanel tuning)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _errors = errors ?? throw new ArgumentNullException(nameof(errors));
        _dataRoot = dataRoot ?? throw new ArgumentNullException(nameof(dataRoot));
        _authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
        _debug = debug ?? throw new ArgumentNullException(nameof(debug));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _settings = EditorInterface.Singleton.GetEditorSettings();
        _layout = LoadStoredLayout();

        _authoringHandle = CreateHandle(ToolboxPanelKind.MoveAuthoring,
            onSuspend: () => _authoring.Visible = false,
            onResume: () => _authoring.Visible = true,
            onShutdown: null,
            onRevalidate: null);
        _debugHandle = CreateHandle(ToolboxPanelKind.EventBusDebug,
            onSuspend: () => { _debug.Visible = false; _debug.SetActive(false); },
            onResume: () => { _debug.Visible = true; _debug.SetActive(true); },
            onShutdown: () => _debug.SetActive(false),
            onRevalidate: null);
        _tuningHandle = CreateHandle(ToolboxPanelKind.RuntimeTuning,
            onSuspend: () => { _tuning.Visible = false; _tuning.SetSuspended(true); },
            onResume: () => { _tuning.Visible = true; _tuning.SetSuspended(false); },
            onShutdown: () => _tuning.Shutdown(),
            onRevalidate: () => _tuning.ReconstructForLifecycle("lifecycle boundary"));

        _authoring.SelectionChanged += id =>
        {
            if (id is not null)
                _selection.PropagateSelection(new ToolboxSelection(
                    ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, id));
        };
        _authoring.ErrorReported += message =>
            _errors.ReportError(ToolboxPanelKind.MoveAuthoring, "authoring", message);
        _tuning.ErrorReported += message =>
            _errors.ReportError(ToolboxPanelKind.RuntimeTuning, "tuning", message);
        _selection.Subscribe(ToolboxPanelKind.RuntimeTuning,
            selection => _tuning.SelectMoveItem(selection.ItemId));
        _errors.ErrorReported += error =>
            _statusLabel.Text = $"[{error.Source}] {error.Operation}: {error.Message}";
        _workspace.BoundaryTraced += message => GD.Print(message);

        BuildUi();
    }

    public override void _Ready()
    {
        // Apply the stored layout after the first layout pass so split offsets
        // are computed against the real dock width.
        GetTree().CreateTimer(0.0f).Timeout += ApplyLayoutRestore;
    }

    /// <summary>
    /// Activates the hosted panels against the restored layout, starts the
    /// editor-process FileWatcher (AC03 observability), and syncs the initial
    /// move selection. Must be called once after the dock is in the tree.
    /// </summary>
    public void Open()
    {
        _workspace.Activate(_authoringHandle);
        _workspace.Activate(_debugHandle);
        _workspace.Activate(_tuningHandle);
        foreach (ToolboxPanelKind kind in AllKinds)
        {
            bool visible = ToolboxLayoutService.EntryFor(_layout, kind).Visible;
            if (visible)
                ActivatePanelControl(kind);
            else
                _workspace.SetPanelVisible(kind, false);
        }
        _watcher = new FileWatcher(_dataRoot, "*.json");
        if (_authoring.SelectedMoveId is { } moveId)
            _selection.PropagateSelection(new ToolboxSelection(
                ToolboxPanelKind.MoveAuthoring, ToolboxSelectionIdentityKind.Move, moveId));
        _statusLabel.Text = "Toolbox ready.";
    }

    /// <summary>
    /// Exactly-once teardown (S4.3-AC13): all fallible operations (watcher
    /// dispose, layout persist) run before the workspace's first committed
    /// release; everything after Close is non-fallible.
    /// </summary>
    public void Shutdown()
    {
        if (_shutdown)
            return;
        _shutdown = true;
        UpdateProportionsFromCurrentSizes();
        _watcher?.Dispose();
        _watcher = null;
        PersistLayout();
        _workspace.Close();
    }

    public override void _ExitTree() => Shutdown();

    private void BuildUi()
    {
        Name = "FTG Framework Toolbox";
        CustomMinimumSize = new Vector2(760, 360);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Top bar: title and panel toggles flow naturally and wrap at narrow dock
        // widths or when more panel kinds are added (content-sized, no ExpandFill
        // title hogging the row). The dock's native title bar already shows the
        // toolbox name, so the in-bar title stays compact.
        var toolbar = new HFlowContainer();
        toolbar.AddChild(new Label { Text = "FTG Framework Toolbox", MouseFilter = MouseFilterEnum.Ignore });
        foreach (ToolboxPanelKind kind in AllKinds)
        {
            ToolboxPanelLayoutEntry entry = ToolboxLayoutService.EntryFor(_layout, kind);
            var toggle = new CheckButton
            {
                Text = kind switch
                {
                    ToolboxPanelKind.MoveAuthoring => "Authoring",
                    ToolboxPanelKind.EventBusDebug => "EventBus Debug",
                    _ => "Runtime Tuning"
                },
                ButtonPressed = entry.Visible,
                TooltipText = "Show or hide this panel; hidden panels suspend all subscriptions (S4.3-AC06)."
            };
            toggle.Toggled += pressed => TogglePanel(kind, pressed);
            toolbar.AddChild(toggle);
        }
        // The status label must fill the row's remaining width: with WordSmart
        // autowrap its minimum width shrinks to the longest word, so without
        // ExpandFill the flow/hbox container would size it to a narrow sliver
        // and the text would wrap vertically ("Toolbox / ready.").
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        toolbar.AddChild(_statusLabel);
        AddChild(toolbar);

        _outerSplit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _outerSplit.SizeFlagsVertical = SizeFlags.ExpandFill;
        _innerSplit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _innerSplit.SizeFlagsVertical = SizeFlags.ExpandFill;
        _outerSplit.Dragged += OnSplitDragged;
        _innerSplit.Dragged += OnSplitDragged;

        _authoring.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _debug.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tuning.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _outerSplit.AddChild(_authoring);
        _innerSplit.AddChild(_debug);
        _innerSplit.AddChild(_tuning);
        _outerSplit.AddChild(_innerSplit);
        AddChild(_outerSplit);
    }

    private static ToolboxPanelHandle CreateHandle(
        ToolboxPanelKind kind,
        Action onSuspend,
        Action onResume,
        Action? onShutdown,
        Action? onRevalidate) => new(kind)
    {
        OnSuspend = onSuspend,
        OnResume = onResume,
        OnShutdown = onShutdown,
        OnRevalidate = onRevalidate
    };

    private void ActivatePanelControl(ToolboxPanelKind kind)
    {
        switch (kind)
        {
            case ToolboxPanelKind.MoveAuthoring:
                _authoring.Visible = true;
                break;
            case ToolboxPanelKind.EventBusDebug:
                _debug.Visible = true;
                _debug.SetActive(true);
                break;
            case ToolboxPanelKind.RuntimeTuning:
                _tuning.Visible = true;
                break;
        }
    }

    private void TogglePanel(ToolboxPanelKind kind, bool visible)
    {
        _workspace.SetPanelVisible(kind, visible);
        _layout = _layout with
        {
            Panels = _layout.Panels
                .Select(entry => entry.Kind == kind ? entry with { Visible = visible } : entry)
                .ToArray()
        };
        PersistLayout();
    }

    private void OnSplitDragged(long offset)
    {
        UpdateProportionsFromCurrentSizes();
        PersistLayout();
    }

    private void ApplyLayoutRestore()
    {
        if (!IsInsideTree()) return;
        ApplyPanelOrder();
        ApplyPanelSizes();
    }

    private void ApplyPanelOrder()
    {
        ToolboxPanelKind[] ordered = _layout.Panels.OrderBy(entry => entry.Order).Select(entry => entry.Kind).ToArray();
        ToolboxPanelKind[] rest = ordered.Where(kind => kind != ToolboxPanelKind.MoveAuthoring).ToArray();
        _outerSplit.MoveChild(ordered[0] == ToolboxPanelKind.MoveAuthoring ? _authoring : _innerSplit, 0);
        _innerSplit.MoveChild(rest[0] == ToolboxPanelKind.EventBusDebug ? _debug : _tuning, 0);
    }

    private void ApplyPanelSizes()
    {
        float total = Math.Max(1f, Size.X);
        float authoringWidth = ToolboxLayoutService.EntryFor(_layout, ToolboxPanelKind.MoveAuthoring).SizeProportion * total;
        float debugWidth = ToolboxLayoutService.EntryFor(_layout, ToolboxPanelKind.EventBusDebug).SizeProportion * total;
        _outerSplit.SetSplitOffset((int)authoringWidth);
        _innerSplit.SetSplitOffset((int)debugWidth);
    }

    private void UpdateProportionsFromCurrentSizes()
    {
        float total = Size.X;
        if (total <= 0f) return;
        float authoring = _authoring.Size.X;
        float debug = _debug.Size.X;
        float tuning = _tuning.Size.X;
        if (authoring + debug + tuning <= 0f) return;
        _layout = _layout with
        {
            Panels = _layout.Panels.Select(entry => entry.Kind switch
            {
                ToolboxPanelKind.MoveAuthoring => entry with { SizeProportion = authoring / total },
                ToolboxPanelKind.EventBusDebug => entry with { SizeProportion = debug / total },
                _ => entry with { SizeProportion = tuning / total }
            }).ToArray()
        };
    }

    private void PersistLayout() =>
        _settings.SetSetting(ToolboxLayoutService.EditorSettingsKey,
            ToolboxLayoutService.Serialize(_layout));

    private WorkspaceLayoutData LoadStoredLayout()
    {
        if (!_settings.HasSetting(ToolboxLayoutService.EditorSettingsKey))
            return ToolboxLayoutService.DefaultLayout();
        string? raw = _settings.GetSetting(ToolboxLayoutService.EditorSettingsKey).AsString();
        return ToolboxLayoutService.Parse(raw);
    }
}
#endif
