#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Core;

namespace FTG_Framework.UI.Training.ViewModels;

/// <summary>
/// Pure save/load ViewModel for training snapshots (Story 2.5). Lists and selects
/// valid saves, requests capture/restore through the TrainingStateService, and
/// reports success only after atomic persistence or restore completion. All
/// behavior is testable without Godot; the thin adapter supplies name→path
/// resolution, save listing, and busy-state rendering.
/// </summary>
public sealed class TrainingSaveLoadViewModel
{
    private readonly TrainingStateService? _service;
    private readonly Func<string, string> _resolvePath;
    private readonly Func<IReadOnlyList<string>> _listSaves;
    private readonly Func<string, string> _canonicalize;
    private string? _pendingSaveName;
    private string? _pendingLoadName;
    private bool _busy;

    public TrainingSaveLoadViewModel(TrainingStateService? service,
        Func<string, string>? resolvePath = null,
        Func<IReadOnlyList<string>>? listSaves = null,
        Func<string, string>? canonicalize = null)
    {
        _service = service;
        _resolvePath = resolvePath ?? (name => name);
        _listSaves = listSaves ?? (() => Array.Empty<string>());
        _canonicalize = canonicalize ?? (name => name);
    }

    private static bool NameEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> Saves { get; private set; } = Array.Empty<string>();
    public string? SelectedSave { get; private set; }
    public TrainingStateService.TrainingSaveSlotInfo? SelectedMetadata { get; private set; }
    public string StatusText { get; private set; } = "Ready";
    public bool IsBusy => _busy;
    public bool HasPendingSave => _pendingSaveName is not null;
    public bool HasPendingLoad => _pendingLoadName is not null;
    public string? PendingSaveName => _pendingSaveName;
    public string? PendingLoadName => _pendingLoadName;

    public void Refresh()
    {
        Saves = _listSaves()
            .Select(_canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (SelectedSave is not null && !Saves.Any(name => NameEquals(name, SelectedSave)))
        {
            SelectedSave = null;
            SelectedMetadata = null;
        }
    }

    public bool Select(string name)
    {
        string canonical = _canonicalize(name);
        Refresh();
        if (!Saves.Any(existing => NameEquals(existing, canonical)))
        {
            StatusText = $"[Save] Save '{name}' was not found.";
            return false;
        }
        SelectedSave = canonical;
        if (_service is null)
        {
            StatusText = $"[Save] Save '{name}' is unavailable without a save service.";
            return false;
        }
        if (_service.TryInspect(_resolvePath(canonical), out TrainingStateService.TrainingSaveSlotInfo? info, out string error))
        {
            SelectedMetadata = info;
            StatusText = $"Inspected '{canonical}': frame {info!.Frame}, epoch {info.SourceEpoch}, {info.ComponentCount} components.";
            return true;
        }
        SelectedMetadata = null;
        StatusText = error;
        return false;
    }

    public bool RequestSave(string name)
    {
        if (!TryValidateName(name)) return false;
        string canonical = _canonicalize(name);
        if (string.IsNullOrWhiteSpace(canonical))
        {
            StatusText = "[Save] Save name contains no usable characters.";
            return false;
        }
        Refresh();
        _pendingLoadName = null;
        bool exists = Saves.Any(existing => NameEquals(existing, canonical));
        if (exists)
        {
            _pendingSaveName = canonical;
            StatusText = $"Overwrite existing save '{canonical}'?";
            return false;
        }
        return ExecuteSave(canonical);
    }

    public bool ConfirmPendingSave()
    {
        if (_pendingSaveName is null)
        {
            StatusText = "[Save] No save overwrite is pending.";
            return false;
        }
        string name = _pendingSaveName;
        _pendingSaveName = null;
        return ExecuteSave(name);
    }

    public bool RequestLoad(string name)
    {
        string canonical = _canonicalize(name);
        Refresh();
        if (!Saves.Any(existing => NameEquals(existing, canonical)))
        {
            StatusText = $"[Save] Save '{name}' was not found.";
            return false;
        }
        _pendingSaveName = null;
        _pendingLoadName = canonical;
        StatusText = $"Load save '{canonical}'?";
        return false;
    }

    public bool ConfirmPendingLoad()
    {
        if (_pendingLoadName is null)
        {
            StatusText = "[Save] No save load is pending.";
            return false;
        }
        string name = _pendingLoadName;
        _pendingLoadName = null;
        if (_service is null)
        {
            StatusText = "[Save] Restore is unavailable without a save service.";
            return false;
        }
        _busy = true;
        try
        {
            bool accepted = _service.TryRestore(_resolvePath(name), out string error);
            StatusText = accepted ? $"Restored training state from '{name}'." : error;
            if (accepted) SelectedMetadata = null;
            return accepted;
        }
        finally { _busy = false; }
    }

    public void CancelPending()
    {
        _pendingSaveName = null;
        _pendingLoadName = null;
        StatusText = "Save/load action canceled.";
    }

    private bool ExecuteSave(string name)
    {
        // Any prior pending action is superseded by this save; a stale
        // confirmation must never fire for a file the user did not confirm.
        _pendingSaveName = null;
        _pendingLoadName = null;
        if (_service is null)
        {
            StatusText = "[Save] Saving is unavailable without a save service.";
            return false;
        }
        _busy = true;
        try
        {
            bool accepted = _service.TrySave(_resolvePath(name), out string error);
            StatusText = accepted ? $"Saved training state as '{name}'." : error;
            if (accepted)
            {
                Refresh();
                SelectedSave = name;
                SelectedMetadata = null;
            }
            return accepted;
        }
        finally { _busy = false; }
    }

    private bool TryValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = "[Save] Save name is required.";
            return false;
        }
        return true;
    }
}
