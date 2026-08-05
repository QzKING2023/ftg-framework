#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.Input;
using FTG_Framework.UI.Training.ViewModels;
using Godot;

namespace FTG_Framework.UI.Training;

public partial class TrainingInputPlaybackPanel : Control
{
    public TrainingInputService? Service { get; set; }
    public TrainingInputRecordingLibrary? Library { get; set; }

    private TrainingInputPlaybackViewModel? _vm;
    private OptionButton? _source;
    private OptionButton? _dummy;
    private LineEdit? _name;
    private OptionButton? _recordings;
    private Label? _status;
    private Label? _shortcuts;
    private Button? _recordToggle;
    private ConfirmationDialog? _replaceDialog;

    public override void _Ready()
    {
        if (Service is null || Library is null)
        {
            FrameworkLog.Error("[Input] Training input panel requires service and library.");
            return;
        }
        _vm = new TrainingInputPlaybackViewModel(
            Library, Service, () => EventBus.Instance.CurrentFrame,
            () => EventBus.Instance.LifecycleEpoch);
        BuildUi();
        RefreshRecordings();
    }

    public void Shutdown() => Service?.CancelForLifecycle();

    public override void _Process(double delta)
    {
        if (_vm is null || _name is null) return;
        if (_vm.ConsumeServiceCompletion(_name.Text))
            RefreshRecordings();
        ShowPendingConfirmationIfNeeded();
        RefreshRecordAction();
        RefreshStatus();
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(rows);
        rows.AddChild(new Label { Text = "Training Input Recording / Playback" });

        _source = PlayerChoice("Source", rows, 1);
        _dummy = PlayerChoice("Dummy", rows, 2);
        _name = new LineEdit { PlaceholderText = "Recording name", MaxLength = 256 };
        AddRow(rows, "Name", _name);
        _recordings = new OptionButton();
        AddRow(rows, "Recording", _recordings);

        var controls = new List<Control> { _source, _dummy, _name, _recordings };
        _recordToggle = AddAction(rows, controls, "Start Recording", () => ExecuteRecordToggle());
        AddAction(rows, controls, "Play Once", () => AssignAndRun(loop: false));
        AddAction(rows, controls, "Loop", () => AssignAndRun(loop: true));
        AddAction(rows, controls, "Stop Playback", () => { _vm?.StopPlayback(); RefreshStatus(); });
        _shortcuts = new Label
        {
            Text = BuildShortcutText(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        rows.AddChild(_shortcuts);
        _status = new Label { Text = "Ready", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        rows.AddChild(_status);
        _replaceDialog = new ConfirmationDialog
        {
            Title = "Replace Recording",
            OkButtonText = "Overwrite",
            CancelButtonText = "Cancel"
        };
        _replaceDialog.Confirmed += ConfirmPendingOverwrite;
        _replaceDialog.Canceled += CancelPendingOverwrite;
        AddChild(_replaceDialog);
        RebuildFocusNavigation(controls);
        RefreshRecordAction();
        _ = InputMap.HasAction("ui_accept");
    }

    private OptionButton PlayerChoice(string label, VBoxContainer parent, int selected)
    {
        var choice = new OptionButton();
        choice.AddItem("P1", 1);
        choice.AddItem("P2", 2);
        choice.Select(selected - 1);
        AddRow(parent, label, choice);
        return choice;
    }

    private static void AddRow(VBoxContainer parent, string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);
        parent.AddChild(row);
    }

    private static Button AddAction(VBoxContainer parent, List<Control> controls,
        string text, Action action)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.All };
        button.Pressed += action;
        parent.AddChild(button);
        controls.Add(button);
        return button;
    }

    private void Run(Func<TrainingInputPlaybackViewModel, bool> action)
    {
        if (_vm is null) return;
        SyncSelections();
        action(_vm);
        RefreshRecordings();
        RefreshStatus();
    }

    private void AssignAndRun(bool loop)
    {
        Run(vm =>
        {
            if (_recordings is null || _recordings.ItemCount == 0) return false;
            string selected = _recordings.GetItemText(_recordings.Selected);
            return vm.Select(selected) && vm.AssignSelected() && vm.StartPlayback(loop);
        });
    }

    private void SyncSelections()
    {
        if (_vm is null || _source is null || _dummy is null) return;
        _vm.SourcePlayer = _source.GetItemId(_source.Selected);
        _vm.DummyPlayer = _dummy.GetItemId(_dummy.Selected);
    }

    private void RefreshRecordings()
    {
        if (_recordings is null || Library is null) return;
        string? selected = _recordings.ItemCount == 0 ? null : _recordings.GetItemText(_recordings.Selected);
        _recordings.Clear();
        foreach (string name in Library.Recordings.Keys.OrderBy(value => value, StringComparer.Ordinal))
            _recordings.AddItem(name);
        if (selected is not null)
        {
            for (int i = 0; i < _recordings.ItemCount; i++)
                if (_recordings.GetItemText(i) == selected) { _recordings.Select(i); break; }
        }
    }

    private void RefreshStatus()
    {
        if (_status is not null && _vm is not null) _status.Text = _vm.StatusText;
    }

    internal bool ExecuteShortcut(TrainingShortcutCommand command)
    {
        if (_vm is null) return false;
        SyncSelections();
        bool accepted = command switch
        {
            TrainingShortcutCommand.RecordToggle => ExecuteRecordToggle(),
            TrainingShortcutCommand.PlayOnce => AssignAndRunShortcut(loop: false),
            TrainingShortcutCommand.LoopToggle => _vm.IsPlaying
                ? StopPlaybackShortcut()
                : AssignAndRunShortcut(loop: true),
            TrainingShortcutCommand.StopPlayback => StopPlaybackShortcut(),
            _ => false
        };
        RefreshRecordings();
        RefreshStatus();
        RefreshRecordAction();
        return accepted;
    }

    internal void ShowShortcutConflict(string conflict)
    {
        if (_status is not null) _status.Text = conflict;
    }

    private bool AssignAndRunShortcut(bool loop)
    {
        if (_vm is null || _recordings is null || _recordings.ItemCount == 0)
        {
            _vm?.Select(string.Empty);
            return false;
        }
        string selected = _recordings.GetItemText(_recordings.Selected);
        return _vm.Select(selected) && _vm.AssignSelected() && _vm.StartPlayback(loop);
    }

    private bool StopPlaybackShortcut()
    {
        if (_vm is null) return false;
        _vm.StopPlayback();
        return true;
    }

    private bool ExecuteRecordToggle()
    {
        if (_vm is null) return false;
        SyncSelections();
        bool accepted = ExecuteRecordToggleCommand(_vm, _name?.Text ?? string.Empty);
        RefreshRecordings();
        ShowPendingConfirmationIfNeeded();
        RefreshRecordAction();
        RefreshStatus();
        return accepted;
    }

    internal static bool ExecuteRecordToggleCommand(
        TrainingInputPlaybackViewModel viewModel, string name)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return viewModel.ToggleCapture(name);
    }

    private void ShowPendingConfirmationIfNeeded()
    {
        if (_vm?.PendingRecording is not TrainingInputRecording pending ||
            Library is null || !Library.Recordings.ContainsKey(pending.Name) ||
            _replaceDialog?.Visible == true) return;
        _replaceDialog!.DialogText = $"Replace recording '{pending.Name}' with the new content?";
        _replaceDialog.PopupCentered();
    }

    private void ConfirmPendingOverwrite()
    {
        _vm?.ConfirmPendingOverwrite();
        RefreshRecordings();
        RefreshRecordAction();
        RefreshStatus();
    }

    private void CancelPendingOverwrite()
    {
        _vm?.CancelPending();
        RefreshRecordAction();
        RefreshStatus();
    }

    private void RefreshRecordAction()
    {
        if (_recordToggle is null || _vm is null) return;
        _recordToggle.Text = _vm.RecordActionText;
        _recordToggle.TooltipText = _vm.RecordActionText;
    }

    private static string BuildShortcutText() =>
        $"Record: {TrainingShortcutRouter.DescribeBindings(TrainingShortcutRouter.RecordToggleAction)}\n" +
        $"Play once: {TrainingShortcutRouter.DescribeBindings(TrainingShortcutRouter.PlayOnceAction)}\n" +
        $"Loop: {TrainingShortcutRouter.DescribeBindings(TrainingShortcutRouter.LoopToggleAction)}\n" +
        $"Stop: {TrainingShortcutRouter.DescribeBindings(TrainingShortcutRouter.StopPlaybackAction)}";

    private static void RebuildFocusNavigation(IReadOnlyList<Control> controls)
    {
        for (int i = 0; i < controls.Count; i++)
        {
            Control previous = controls[(i - 1 + controls.Count) % controls.Count];
            Control next = controls[(i + 1) % controls.Count];
            controls[i].FocusNeighborTop = controls[i].GetPathTo(previous);
            controls[i].FocusNeighborBottom = controls[i].GetPathTo(next);
            controls[i].FocusPrevious = controls[i].GetPathTo(previous);
            controls[i].FocusNext = controls[i].GetPathTo(next);
        }
    }
}
