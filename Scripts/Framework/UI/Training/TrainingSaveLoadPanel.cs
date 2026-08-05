#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.UI.Training.ViewModels;
using Godot;

namespace FTG_Framework.UI.Training;

/// <summary>
/// Thin Godot adapter for training snapshot save/load. All validation,
/// persistence, and restore orchestration lives in the pure ViewModel and
/// TrainingStateService; this panel only resolves user:// paths, builds the
/// controls, and renders status. The Godot <c>user://</c> base path is resolved
/// here (ProjectSettings.GlobalizePath) so the persistence layer stays testable
/// under dotnet test.
/// </summary>
public partial class TrainingSaveLoadPanel : Control
{
    private const string SavesSubdirectory = "training-saves";

    public TrainingStateService? Service { get; set; }

    private TrainingSaveLoadViewModel? _viewModel;
    private LineEdit? _nameEntry;
    private ItemList? _saveList;
    private Label? _status;
    private ConfirmationDialog? _confirmDialog;
    private bool _confirmIsSave;

    public override void _Ready()
    {
        if (Service is null)
            return;
        _viewModel = new TrainingSaveLoadViewModel(Service, ResolvePath, ListSaveNames, SanitizeName);
        BuildUi();
        _viewModel.Refresh();
    }

    public override void _Process(double delta)
    {
        if (_viewModel is null || _status is null)
            return;
        _status.Text = _viewModel.StatusText;
    }

    public void Shutdown()
    {
        _viewModel = null;
    }

    private void BuildUi()
    {
        var root = new VBoxContainer { Name = "SaveLoadPanel" };
        AddChild(root);

        var nameRow = new HBoxContainer { Name = "SaveNameRow" };
        root.AddChild(nameRow);
        _nameEntry = new LineEdit
        {
            Name = "SaveNameEntry",
            PlaceholderText = "Save name",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        nameRow.AddChild(_nameEntry);
        var saveButton = new Button
        {
            Name = "SaveButton",
            Text = "Save",
            FocusMode = Control.FocusModeEnum.All
        };
        saveButton.Pressed += OnSavePressed;
        nameRow.AddChild(saveButton);

        _saveList = new ItemList
        {
            Name = "SaveList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All
        };
        _saveList.ItemSelected += OnSaveSelected;
        root.AddChild(_saveList);

        var actionRow = new HBoxContainer { Name = "SaveLoadActionsRow" };
        root.AddChild(actionRow);
        var loadButton = new Button
        {
            Name = "LoadButton",
            Text = "Load Selected",
            FocusMode = Control.FocusModeEnum.All
        };
        loadButton.Pressed += OnLoadPressed;
        actionRow.AddChild(loadButton);
        var refreshButton = new Button
        {
            Name = "RefreshSavesButton",
            Text = "Refresh",
            FocusMode = Control.FocusModeEnum.All
        };
        refreshButton.Pressed += OnRefreshPressed;
        actionRow.AddChild(refreshButton);
        var cancelButton = new Button
        {
            Name = "CancelPendingButton",
            Text = "Cancel",
            FocusMode = Control.FocusModeEnum.All
        };
        cancelButton.Pressed += OnCancelPressed;
        actionRow.AddChild(cancelButton);

        _status = new Label
        {
            Name = "SaveLoadStatus",
            Text = "Ready",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_status);

        _confirmDialog = new ConfirmationDialog
        {
            Name = "SaveLoadConfirmDialog",
            OkButtonText = "Confirm"
        };
        _confirmDialog.Confirmed += OnConfirmDialogConfirmed;
        _confirmDialog.Canceled += OnConfirmDialogCanceled;
        AddChild(_confirmDialog);

        RefreshList();
    }

    private void OnSavePressed()
    {
        if (_viewModel is null) return;
        string name = _nameEntry?.Text?.Trim() ?? string.Empty;
        _viewModel.RequestSave(name);
        if (_viewModel.HasPendingSave)
            ShowConfirm($"Overwrite existing save '{name}'?");
        else
            RefreshList();
    }

    private void OnLoadPressed()
    {
        if (_viewModel is null || _saveList is null) return;
        if (_saveList.GetSelectedItems().Length == 0)
        {
            _viewModel.Select(string.Empty);
            return;
        }
        string name = _saveList.GetItemText(_saveList.GetSelectedItems()[0]);
        _viewModel.RequestLoad(name);
        if (_viewModel.HasPendingLoad)
            ShowConfirm($"Load save '{name}'?");
    }

    private void OnRefreshPressed()
    {
        _viewModel?.Refresh();
        RefreshList();
    }

    private void OnCancelPressed()
    {
        _viewModel?.CancelPending();
        RefreshList();
    }

    private void OnSaveSelected(long index)
    {
        if (_viewModel is null || _saveList is null)
            return;
        _viewModel.Select(_saveList.GetItemText((int)index));
    }

    private void OnConfirmDialogConfirmed()
    {
        if (_viewModel is null)
            return;
        if (_confirmIsSave)
            _viewModel.ConfirmPendingSave();
        else
            _viewModel.ConfirmPendingLoad();
        RefreshList();
    }

    private void OnConfirmDialogCanceled()
    {
        _viewModel?.CancelPending();
    }

    private void ShowConfirm(string message)
    {
        if (_confirmDialog is null)
            return;
        _confirmIsSave = _viewModel?.HasPendingSave == true;
        _confirmDialog.DialogText = message;
        _confirmDialog.PopupCentered();
    }

    private void RefreshList()
    {
        if (_viewModel is null || _saveList is null)
            return;
        _saveList.Clear();
        foreach (string name in _viewModel.Saves)
            _saveList.AddItem(name);
    }

    private string ResolvePath(string name)
    {
        string directory = SavesDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, SanitizeName(name) + ".json");
    }

    private static IReadOnlyList<string> ListSaveNames()
    {
        string directory = SavesDirectory();
        if (!Directory.Exists(directory))
            return Array.Empty<string>();
        // Only names that round-trip through the canonical form are operable;
        // hand-placed files with sanitized-away characters would list but never
        // resolve to a loadable path.
        return Directory.GetFiles(directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                          string.Equals(name, SanitizeName(name), StringComparison.Ordinal))
            .ToArray()!;
    }

    private static string SavesDirectory() =>
        Path.Combine(ProjectSettings.GlobalizePath("user://"), SavesSubdirectory);

    private static string SanitizeName(string name)
    {
        var characters = name.Where(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
        string sanitized = new string(characters.ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "save" : sanitized;
    }
}
