#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training.ViewModels;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class RuntimeTuningPanel : Control
{
    public IRuntimeTuningService? Service { get; set; }
    public IDataStore? DataStore { get; set; }
    public RuntimeTuningSessionAuthority? Sessions { get; set; }

    private RuntimeTuningViewModel? _viewModel;
    private ScrollContainer? _scroll;
    private OptionButton? _kind;
    private OptionButton? _item;
    private VBoxContainer? _fields;
    private Label? _status;
    private Button? _apply;
    private Button? _reload;
    private Button? _reapply;
    private readonly Dictionary<string, LineEdit> _editors = new(StringComparer.Ordinal);
    private bool _disposed;

    internal RuntimeTuningViewModel? ViewModel => _viewModel;

    public override void _Ready()
    {
        if (Service is null || DataStore is null || Sessions is null)
        {
            GD.PushError("[RuntimeTuningPanel] Service, DataStore, and Sessions are required.");
            return;
        }
        CustomMinimumSize = new Vector2(360, 320);
        Position = new Vector2(620, 10);
        _viewModel = new RuntimeTuningViewModel(Service, Sessions);
        EnsureControllerActions();

        _scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(360, 320),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        AddChild(_scroll);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(340, 300) };
        _scroll.AddChild(root);
        root.AddChild(new Label { Text = "Runtime Tuning (next initiation)" });

        _kind = new OptionButton { FocusMode = FocusModeEnum.All };
        _kind.AddItem("Move", (int)RuntimeTuningSelectionKind.Move);
        _kind.AddItem("Knockback Profile", (int)RuntimeTuningSelectionKind.KnockbackProfile);
        _kind.AddItem("Physics Response", (int)RuntimeTuningSelectionKind.PhysicsResponseProfile);
        _kind.ItemSelected += _ => RebuildItems();
        root.AddChild(_kind);
        RegisterScrollableFocus(_kind);

        _item = new OptionButton { FocusMode = FocusModeEnum.All };
        _item.ItemSelected += _ => OpenSelection();
        root.AddChild(_item);
        RegisterScrollableFocus(_item);

        _fields = new VBoxContainer();
        root.AddChild(_fields);

        var actions = new HBoxContainer();
        _apply = new Button { Text = "Apply / Write", FocusMode = FocusModeEnum.All };
        _apply.Pressed += Apply;
        actions.AddChild(_apply);
        RegisterScrollableFocus(_apply);
        _reload = new Button { Text = "Reload", FocusMode = FocusModeEnum.All };
        _reload.Pressed += Reload;
        actions.AddChild(_reload);
        RegisterScrollableFocus(_reload);
        var reapply = new Button { Text = "Reapply…", FocusMode = FocusModeEnum.All };
        _reapply = reapply;
        _reapply.Pressed += ConfirmReapply;
        actions.AddChild(_reapply);
        RegisterScrollableFocus(_reapply);
        root.AddChild(actions);

        _status = new Label { Text = "Select committed data to begin." };
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_status);

        EventBus.Instance.Subscribe<MatchInitializedEvent>(OnLifecycle);
        EventBus.Instance.Subscribe<ReplayStartedEvent>(OnLifecycle);
        EventBus.Instance.Subscribe<StateRestoredEvent>(OnLifecycle);
        RebuildItems();
        RestoreFocus(_kind);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("tuning_apply")) { Apply(); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("tuning_reload")) { Reload(); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("tuning_reapply")) { ConfirmReapply(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree() => Shutdown();

    internal void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        EventBus.Instance.Unsubscribe<MatchInitializedEvent>(OnLifecycle);
        EventBus.Instance.Unsubscribe<ReplayStartedEvent>(OnLifecycle);
        EventBus.Instance.Unsubscribe<StateRestoredEvent>(OnLifecycle);
        _apply?.SetDisabled(true);
        _viewModel?.Dispose();
        _viewModel = null;
    }

    private RuntimeTuningSelectionKind SelectedKind => _kind is null
        ? RuntimeTuningSelectionKind.Move
        : (RuntimeTuningSelectionKind)_kind.GetSelectedId();

    private void RebuildItems()
    {
        if (_item is null || DataStore is null) return;
        _item.Clear();
        IEnumerable<string> ids = SelectedKind switch
        {
            RuntimeTuningSelectionKind.Move => DataStore.GetAllMoves().Select(item => item.MoveId),
            RuntimeTuningSelectionKind.KnockbackProfile => DataStore.GetAllKnockbackProfiles().Select(item => item.ProfileId),
            _ => DataStore.GetAllPhysicsResponseProfiles().Select(item => item.ProfileId)
        };
        foreach (string id in ids.OrderBy(id => id, StringComparer.Ordinal)) _item.AddItem(id);
        OpenSelection();
        RebuildFocusNavigation();
        RestoreFocus(_item);
    }

    private void OpenSelection()
    {
        if (_viewModel is null || _item is null || _item.ItemCount == 0) return;
        string id = _item.GetItemText(_item.Selected);
        string document = SelectedKind switch
        {
            RuntimeTuningSelectionKind.Move => "example_moves",
            RuntimeTuningSelectionKind.KnockbackProfile => "knockback",
            _ => "response"
        };
        try
        {
            _viewModel.Open(new RuntimeTuningSelection(SelectedKind, document, id));
            RebuildFields();
            SetStatus($"Loaded committed version {_viewModel.ExpectedDatasetVersion} ({Short(_viewModel.ExpectedIdentity)}).", false);
        }
        catch (Exception ex) { SetStatus($"Load failed: {ex.Message}", true); }
    }

    private void RebuildFields()
    {
        if (_fields is null || _viewModel is null) return;
        foreach (Node child in _fields.GetChildren()) child.QueueFree();
        _editors.Clear();
        foreach ((string key, string value) in _viewModel.CandidateFields)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = key, CustomMinimumSize = new Vector2(150, 0) });
            var editor = new LineEdit { Text = value, FocusMode = FocusModeEnum.All, TooltipText = key };
            editor.TextChanged += text => OnEdit(key, text);
            row.AddChild(editor);
            _fields.AddChild(row);
            _editors[key] = editor;
            RegisterScrollableFocus(editor);
        }
        RebuildFocusNavigation();
        RestoreFocus(_editors.Values.FirstOrDefault());
    }

    private void OnEdit(string key, string text)
    {
        if (_viewModel is null) return;
        _viewModel.EditField(key, text);
        if (_viewModel.Validation.Success) SetStatus("Candidate valid; not yet committed.", false);
        else SetStatus($"Validation: {_viewModel.Validation.Errors.Count} error(s). First: {_viewModel.Validation.FirstInvalidField}", true);
    }

    private void Apply()
    {
        if (_viewModel is null) return;
        RuntimeTuningCommitResult result = _viewModel.Apply();
        SetStatus(Describe(result), !result.Committed);
        if (result.Committed) RebuildFields();
        else if (result.Errors?.FirstOrDefault() is { } error && _editors.TryGetValue(error.FieldPath, out LineEdit? editor))
            RestoreFocus(editor);
    }

    private void Reload()
    {
        if (_viewModel is null) return;
        _viewModel.Reload();
        RebuildFields();
        SetStatus("Reloaded authoritative committed values; staged edits were discarded.", false);
    }

    private void ConfirmReapply()
    {
        if (_viewModel is null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Reapply staged tuning",
            DialogText = $"Reapply staged values over newly loaded '{_viewModel.Selection?.ItemId}'? Unseen committed values will not be overwritten until Apply / Write."
        };
        dialog.Confirmed += () =>
        {
            _viewModel.Reapply();
            RebuildFields();
            SetStatus("Reapplied staged values onto the latest baseline; review and Apply / Write.", false);
            dialog.QueueFree();
        };
        dialog.Canceled += () => { RestoreFocus(_apply); dialog.QueueFree(); };
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnLifecycle(MatchInitializedEvent _) => ReconstructForLifecycle("match restart");
    private void OnLifecycle(ReplayStartedEvent _) => ReconstructForLifecycle("replay start");
    private void OnLifecycle(StateRestoredEvent _) => ReconstructForLifecycle("state restore");
    private void ReconstructForLifecycle(string reason)
    {
        if (_viewModel is null) return;
        _viewModel.Invalidate();
        OpenSelection();
        SetStatus($"Session invalidated by {reason}; reconstructed from authoritative Data.", false);
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = error ? $"ERROR — {text}" : $"STATUS — {text}";
        _status.TooltipText = text;
    }

    private void RegisterScrollableFocus(Control control) =>
        control.FocusEntered += () => EnsureFocusVisible(control);

    private void RestoreFocus(Control? control)
    {
        if (control is null) return;
        control.GrabFocus();
        EnsureFocusVisible(control);
    }

    private void EnsureFocusVisible(Control control)
    {
        if (_scroll is null || !IsInstanceValid(_scroll) || !IsInstanceValid(control)) return;
        _scroll.CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, control);
    }

    private void RebuildFocusNavigation()
    {
        var controls = new List<Control>();
        if (_kind is not null) controls.Add(_kind);
        if (_item is not null) controls.Add(_item);
        controls.AddRange(_editors.Values);
        if (_apply is not null) controls.Add(_apply);
        if (_reload is not null) controls.Add(_reload);
        if (_reapply is not null) controls.Add(_reapply);

        for (int index = 0; index < controls.Count; index++)
        {
            Control control = controls[index];
            Control? previous = index > 0 ? controls[index - 1] : null;
            Control? next = index + 1 < controls.Count ? controls[index + 1] : null;

            control.FocusNeighborTop = previous is null ? new NodePath("") : control.GetPathTo(previous);
            control.FocusNeighborBottom = next is null ? new NodePath("") : control.GetPathTo(next);
            control.FocusPrevious = previous is null ? new NodePath("") : control.GetPathTo(previous);
            control.FocusNext = next is null ? new NodePath("") : control.GetPathTo(next);
        }

        if (_apply is not null && _reload is not null && _reapply is not null)
        {
            _apply.FocusNeighborRight = _apply.GetPathTo(_reload);
            _reload.FocusNeighborLeft = _reload.GetPathTo(_apply);
            _reload.FocusNeighborRight = _reload.GetPathTo(_reapply);
            _reapply.FocusNeighborLeft = _reapply.GetPathTo(_reload);
        }
    }

    private static string Describe(RuntimeTuningCommitResult result) => result.Status switch
    {
        RuntimeTuningCommitStatus.Succeeded => "Committed. New initiations use the tuned value.",
        RuntimeTuningCommitStatus.CommittedWithDiagnostic => $"Committed with diagnostic: {result.Diagnostic}",
        RuntimeTuningCommitStatus.Conflict => $"Conflict: expected {Short(result.ExpectedIdentity)}, current {Short(result.CurrentIdentity)}. Reload or explicitly Reapply.",
        RuntimeTuningCommitStatus.ValidationFailed => $"Validation failed: {result.Diagnostic ?? "correct highlighted fields"}",
        RuntimeTuningCommitStatus.Cancelled => "Cancelled because the tuning session is obsolete.",
        RuntimeTuningCommitStatus.IncompatibleVersion => $"Incompatible version: {result.Diagnostic}",
        _ => $"I/O failure: {result.Diagnostic}"
    };

    private static string Short(string? identity) => string.IsNullOrEmpty(identity) ? "unknown" : identity[..Math.Min(12, identity.Length)];

    private static void EnsureControllerActions()
    {
        Add("tuning_apply", JoyButton.A);
        Add("tuning_reload", JoyButton.X);
        Add("tuning_reapply", JoyButton.Y);
        static void Add(string action, JoyButton button)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            if (InputMap.ActionGetEvents(action).OfType<InputEventJoypadButton>().Any(e => e.ButtonIndex == button)) return;
            InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
        }
    }
}
