#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FTG_Framework.Data;
using Godot;

namespace FTG_Framework.Editor;

[Tool]
public partial class MoveAuthoringDock : ScrollContainer
{
    private readonly VBoxContainer _form = new();
    private readonly OptionButton _moveSelector = new();
    private readonly VBoxContainer _cancelRowsHost = new();
    private readonly VBoxContainer _collisionRowsHost = new();
    private readonly Label _status = new() { Text = "Ready — select or create a move." };
    private readonly Dictionary<string, LineEdit> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _fieldErrors = new(StringComparer.Ordinal);
    private readonly List<CancelRow> _cancelRows = new();
    private readonly List<CollisionRow> _collisionRows = new();
    private MoveAuthoringViewModel? _viewModel;
    private MoveAuthoringUndoService? _undo;
    private string? _moveId;
    private bool _busy;
    private bool _loading;

    public MoveAuthoringDock()
    {
        Name = "FTG Move Authoring";
        CustomMinimumSize = new Vector2(420, 300);
        HorizontalScrollMode = ScrollMode.Auto;
        VerticalScrollMode = ScrollMode.Auto;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(_form);
        _form.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _form.AddChild(new Label { Text = "Move Authoring" });

        var moveBar = new HBoxContainer();
        _moveSelector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _moveSelector.TooltipText = "Select the move to edit. Pending form values are retained when switching.";
        _moveSelector.ItemSelected += SelectMove;
        moveBar.AddChild(_moveSelector);
        var addMove = new Button { Text = "Add Move", TooltipText = "Create a new move in this dataset." };
        addMove.Pressed += AddMove;
        moveBar.AddChild(addMove);
        _form.AddChild(moveBar);

        foreach (string field in new[] { "move_id", "move_name", "startup", "active", "recovery", "hit_advantage", "block_advantage", "damage", "chain_repeatable", "knockback_profile_id" })
            AddField(field);

        AddCollectionHeader("Cancel Windows", "Add Window", AddCancelWindow);
        _form.AddChild(_cancelRowsHost);
        AddCollectionHeader("Collision Frames", "Add Frame", AddCollisionFrame);
        _form.AddChild(_collisionRowsHost);

        var save = new Button { Text = "Validate and Save", TooltipText = "Validate the complete move dataset, then save atomically." };
        save.Pressed += ConfirmSave;
        _form.AddChild(save);
        var reload = new Button { Text = "Reload authoritative data", TooltipText = "Discard form edits and reload the current committed file version." };
        reload.Pressed += ReloadCommitted;
        _form.AddChild(reload);
        var reapply = new Button { Text = "Reapply edits on latest data", TooltipText = "Keep form edits, load the latest file version, and validate the rebased candidate." };
        reapply.Pressed += ReapplyCommitted;
        _form.AddChild(reapply);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _form.AddChild(_status);
    }

    internal void Bind(MoveAuthoringViewModel viewModel, MoveAuthoringUndoService undo) => Bind(viewModel, undo, null);

    private void Bind(MoveAuthoringViewModel viewModel, MoveAuthoringUndoService undo, string? preferredMoveId)
    {
        ClearValidationPresentation();
        _viewModel = viewModel;
        _undo = undo;
        _loading = true;
        _moveSelector.Clear();
        for (int i = 0; i < viewModel.CurrentCandidate.Moves.Count; i++)
            _moveSelector.AddItem(viewModel.CurrentCandidate.Moves[i].MoveId);
        int selected = preferredMoveId is null
            ? 0
            : viewModel.CurrentCandidate.Moves.ToList().FindIndex(move => string.Equals(move.MoveId, preferredMoveId, StringComparison.Ordinal));
        if (selected < 0) selected = 0;
        if (viewModel.CurrentCandidate.Moves.Count == 0)
        {
            _moveId = null;
            ClearNestedRows();
            _status.Text = "No moves are present. Choose Add Move to create one.";
            _loading = false;
            return;
        }
        _moveSelector.Select(selected);
        LoadMove(selected);
        _loading = false;
        _status.Text = "Ready — editing committed authoritative data.";
    }

    private void LoadMove(int index)
    {
        if (_viewModel is null || index < 0 || index >= _viewModel.CurrentCandidate.Moves.Count) return;
        MoveAuthoringMove move = _viewModel.CurrentCandidate.Moves[index];
        _moveId = move.MoveId;
        _fields["move_id"].Text = move.MoveId;
        _fields["move_name"].Text = move.MoveName ?? string.Empty;
        _fields["startup"].Text = move.Startup.ToString();
        _fields["active"].Text = move.Active.ToString();
        _fields["recovery"].Text = move.Recovery.ToString();
        _fields["hit_advantage"].Text = move.HitAdvantage.ToString();
        _fields["block_advantage"].Text = move.BlockAdvantage.ToString();
        _fields["damage"].Text = move.Damage.ToString();
        _fields["chain_repeatable"].Text = move.ChainRepeatable ? "true" : "false";
        _fields["knockback_profile_id"].Text = move.KnockbackProfileId;
        BuildCancelRows(move.CancelWindows);
        BuildCollisionRows(move.CollisionFrames);
    }

    private void SelectMove(long index)
    {
        if (_loading || _busy || _viewModel is null) return;
        int previous = FindMoveIndex(_moveId);
        try
        {
            MoveValidationResult form = ApplyFormEdits();
            if (!form.Success) { ApplyValidationPresentation(form, "Cannot switch moves."); return; }
            if (previous >= 0 && _moveId is not null) _moveSelector.SetItemText(previous, _moveId);
            _loading = true;
            LoadMove((int)index);
            _loading = false;
            _status.Text = "Move selected; pending edits to the previous move were retained.";
        }
        catch (Exception ex)
        {
            _loading = true;
            if (previous >= 0) _moveSelector.Select(previous);
            _loading = false;
            _status.Text = $"Cannot switch moves: {ex.Message}";
        }
    }

    private void AddMove()
    {
        if (_busy || _viewModel is null || _undo is null) return;
        try
        {
            MoveValidationResult form = ApplyFormEdits();
            if (!form.Success) { ApplyValidationPresentation(form, "Cannot add a move."); return; }
            var ids = _viewModel.CurrentCandidate.Moves.Select(move => move.MoveId).ToHashSet(StringComparer.Ordinal);
            string id = "new_move";
            for (int suffix = 2; ids.Contains(id); suffix++) id = $"new_move_{suffix}";
            string profile = _viewModel.CurrentCandidate.Moves.FirstOrDefault()?.KnockbackProfileId ?? string.Empty;
            _viewModel.Add(new MoveAuthoringMove(id, 0, 0, 0, 0, 0, 0, false, profile, null, Array.Empty<CancelWindow>(), Array.Empty<CollisionFrameDefinition>()));
            Bind(_viewModel, _undo, id);
            _status.Text = "New move created. Complete required fields before saving.";
        }
        catch (Exception ex) { _status.Text = $"Add move failed: {ex.Message}"; }
    }

    private void AddCancelWindow() => MutateSelected(move => move with
    {
        CancelWindows = move.CancelWindows.Append(new CancelWindow()).ToArray()
    });

    private void RemoveCancelWindow(int index) => MutateSelected(move => move with
    {
        CancelWindows = move.CancelWindows.Where((_, i) => i != index).ToArray()
    });

    private void AddCollisionFrame() => MutateSelected(move => move with
    {
        CollisionFrames = move.CollisionFrames.Append(new CollisionFrameDefinition { Frame = 1 }).ToArray()
    });

    private void RemoveCollisionFrame(int index) => MutateSelected(move => move with
    {
        CollisionFrames = move.CollisionFrames.Where((_, i) => i != index).ToArray()
    });

    private void AddBox(int frameIndex, bool hitbox) => MutateSelected(move =>
    {
        var frames = move.CollisionFrames.ToArray();
        CollisionFrameDefinition frame = frames[frameIndex];
        var box = new CollisionBoxDefinition { BoxId = hitbox ? "hitbox" : "hurtbox" };
        frames[frameIndex] = hitbox
            ? CloneFrame(frame, hitboxes: frame.Hitboxes.Append(box).ToArray())
            : CloneFrame(frame, hurtboxes: frame.Hurtboxes.Append(box).ToArray());
        return move with { CollisionFrames = frames };
    });

    private void RemoveBox(int frameIndex, bool hitbox, int boxIndex) => MutateSelected(move =>
    {
        var frames = move.CollisionFrames.ToArray();
        CollisionFrameDefinition frame = frames[frameIndex];
        frames[frameIndex] = hitbox
            ? CloneFrame(frame, hitboxes: frame.Hitboxes.Where((_, i) => i != boxIndex).ToArray())
            : CloneFrame(frame, hurtboxes: frame.Hurtboxes.Where((_, i) => i != boxIndex).ToArray());
        return move with { CollisionFrames = frames };
    });

    private void MutateSelected(Func<MoveAuthoringMove, MoveAuthoringMove> mutation)
    {
        if (_busy || _viewModel is null || _undo is null || _moveId is null) return;
        try
        {
            MoveValidationResult form = ApplyFormEdits();
            if (!form.Success) { ApplyValidationPresentation(form, "Cannot edit this collection."); return; }
            string id = _moveId;
            _viewModel.Edit(id, mutation);
            Bind(_viewModel, _undo, id);
        }
        catch (Exception ex) { _status.Text = $"Edit failed: {ex.Message}"; }
    }

    private void ReloadCommitted()
    {
        if (_busy || _viewModel is null || _undo is null) return;
        try { _viewModel.ReloadCommitted(); Bind(_viewModel, _undo, _moveId); _status.Text = "Reloaded the latest committed data; form edits were discarded."; }
        catch (Exception ex) { _status.Text = $"Reload failed: {ex.Message}"; }
    }

    private void ReapplyCommitted()
    {
        if (_busy || _viewModel is null || _undo is null || _moveId is null) return;
        try
        {
            MoveValidationResult form = ApplyFormEdits();
            if (!form.Success) { ApplyValidationPresentation(form, "Cannot reapply malformed form values."); return; }
            string editedId = _moveId;
            _viewModel.ReapplyCommitted();
            Bind(_viewModel, _undo, editedId);
            if (_viewModel.LastResult.Success)
                _status.Text = "Reapplied form edits on the latest committed data. Validate and Save to commit.";
            else
                ApplyValidationPresentation(_viewModel.LastResult, "Reapply needs correction.");
        }
        catch (Exception ex) { _status.Text = $"Reapply failed: {ex.Message}"; }
    }

    private void ConfirmSave()
    {
        if (_busy || _viewModel is null || _undo is null || _moveId is null) return;
        var confirm = new ConfirmationDialog { Title = "Replace move dataset", DialogText = $"Validate and atomically replace the dataset containing '{_moveId}'?", OkButtonText = "Validate and Save" };
        AddChild(confirm);
        confirm.Confirmed += () => { Save(); confirm.QueueFree(); };
        confirm.Canceled += confirm.QueueFree;
        confirm.PopupCentered();
    }

    private void Save()
    {
        if (_viewModel is null || _undo is null || _moveId is null) return;
        _busy = true;
        _status.Text = "Validating…";
        try
        {
            MoveValidationResult form = ApplyFormEdits();
            if (!form.Success) { ApplyValidationPresentation(form, "Validation failed."); return; }
            string savedMoveId = _moveId;
            MoveValidationResult validation = _viewModel.Validate();
            if (!validation.Success)
            {
                ApplyValidationPresentation(validation, "Validation failed.");
                return;
            }
            ClearValidationPresentation();
            _undo.SaveUndoable(_viewModel.CurrentCandidate);
            MoveSaveResult? result = _undo.LastResult;
            if (result?.Status == MoveSaveStatus.Succeeded)
            {
                _viewModel.ReloadCommitted();
                Bind(_viewModel, _undo, savedMoveId);
                _status.Text = "Saved successfully.";
            }
            else if (result?.Status == MoveSaveStatus.ValidationFailed && result.Errors is { Count: > 0 })
            {
                ApplyValidationPresentation(new MoveValidationResult(result.Errors), "Validation failed.");
            }
            else
            {
                _status.Text = result?.Status == MoveSaveStatus.Conflict
                    ? $"Conflict: expected {result.ExpectedIdentity?.Sha256}, current {result.CurrentIdentity?.Sha256}. Reload or reapply before saving."
                    : $"Save failed: {result?.Diagnostic ?? "validation or I/O error"}";
            }
        }
        catch (Exception ex) { _status.Text = $"Save failed: {ex.Message}"; }
        finally { _busy = false; }
    }

    private MoveValidationResult ApplyFormEdits()
    {
        if (_viewModel is null || _moveId is null) return MoveValidationResult.Valid;
        string originalId = _moveId;
        string editedId = _fields["move_id"].Text;
        int moveIndex = Math.Max(0, FindMoveIndex(originalId));
        MoveAuthoringScalarParseResult parsed = MoveAuthoringScalarParser.Parse(
            _fields.ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal), moveIndex);
        if (!parsed.Validation.Success) return parsed.Validation;
        MoveAuthoringScalarValues values = parsed.Values!;
        _viewModel.Edit(originalId, move => move with
        {
            MoveId = editedId,
            MoveName = string.IsNullOrEmpty(_fields["move_name"].Text) ? null : _fields["move_name"].Text,
            Startup = values.Startup, Active = values.Active, Recovery = values.Recovery,
            HitAdvantage = values.HitAdvantage, BlockAdvantage = values.BlockAdvantage, Damage = values.Damage,
            ChainRepeatable = values.ChainRepeatable, KnockbackProfileId = _fields["knockback_profile_id"].Text,
            CancelWindows = _cancelRows.Select(row => new CancelWindow { StartFrame = (int)row.Start.Value, EndFrame = (int)row.End.Value, TargetCategory = row.Category.Text }).ToArray(),
            CollisionFrames = _collisionRows.Select(ReadCollisionFrame).ToArray()
        });
        _moveId = editedId;
        return MoveValidationResult.Valid;
    }

    private static CollisionFrameDefinition ReadCollisionFrame(CollisionRow row) => new()
    {
        Frame = (int)row.Frame.Value,
        Hitboxes = row.Hitboxes.Select(ReadBox).ToArray(),
        Hurtboxes = row.Hurtboxes.Select(ReadBox).ToArray()
    };

    private static CollisionBoxDefinition ReadBox(BoxRow row) => new()
    {
        BoxId = row.Id.Text, X = (float)row.X.Value, Y = (float)row.Y.Value,
        Width = (float)row.Width.Value, Height = (float)row.Height.Value
    };

    private void BuildCancelRows(IReadOnlyList<CancelWindow> windows)
    {
        ClearChildren(_cancelRowsHost);
        _cancelRows.Clear();
        for (int i = 0; i < windows.Count; i++)
        {
            int index = i;
            var row = new HBoxContainer();
            var start = Number(windows[i].StartFrame, 0, int.MaxValue);
            var end = Number(windows[i].EndFrame, 0, int.MaxValue);
            var category = new LineEdit { Text = windows[i].TargetCategory, PlaceholderText = "target_category", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(new Label { Text = "start" }); row.AddChild(start);
            row.AddChild(new Label { Text = "end" }); row.AddChild(end);
            row.AddChild(category);
            var remove = new Button { Text = "Remove" }; remove.Pressed += () => RemoveCancelWindow(index); row.AddChild(remove);
            _cancelRowsHost.AddChild(row);
            var error = ErrorLabel(); _cancelRowsHost.AddChild(error);
            _cancelRows.Add(new CancelRow(start, end, category, error));
        }
    }

    private void BuildCollisionRows(IReadOnlyList<CollisionFrameDefinition> frames)
    {
        ClearChildren(_collisionRowsHost);
        _collisionRows.Clear();
        for (int i = 0; i < frames.Count; i++)
        {
            int frameIndex = i;
            var panel = new VBoxContainer();
            var header = new HBoxContainer();
            var frame = Number(frames[i].Frame, 1, int.MaxValue);
            header.AddChild(new Label { Text = "frame" }); header.AddChild(frame);
            var addHit = new Button { Text = "Add Hitbox" }; addHit.Pressed += () => AddBox(frameIndex, true); header.AddChild(addHit);
            var addHurt = new Button { Text = "Add Hurtbox" }; addHurt.Pressed += () => AddBox(frameIndex, false); header.AddChild(addHurt);
            var remove = new Button { Text = "Remove Frame" }; remove.Pressed += () => RemoveCollisionFrame(frameIndex); header.AddChild(remove);
            panel.AddChild(header);
            var error = ErrorLabel(); panel.AddChild(error);
            var hitRows = new List<BoxRow>();
            AddBoxRows(panel, frames[i].Hitboxes, hitRows, frameIndex, true, "Hitbox");
            var hurtRows = new List<BoxRow>();
            AddBoxRows(panel, frames[i].Hurtboxes, hurtRows, frameIndex, false, "Hurtbox");
            _collisionRowsHost.AddChild(panel);
            _collisionRows.Add(new CollisionRow(frame, hitRows, hurtRows, error));
        }
    }

    private void AddBoxRows(VBoxContainer host, IReadOnlyList<CollisionBoxDefinition> boxes, List<BoxRow> rows, int frameIndex, bool hitbox, string label)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            int boxIndex = i;
            var row = new HBoxContainer();
            var id = new LineEdit { Text = boxes[i].BoxId, PlaceholderText = "box_id", CustomMinimumSize = new Vector2(90, 0) };
            var x = Number(boxes[i].X, float.MinValue, float.MaxValue);
            var y = Number(boxes[i].Y, float.MinValue, float.MaxValue);
            var width = Number(boxes[i].Width, 0, float.MaxValue);
            var height = Number(boxes[i].Height, 0, float.MaxValue);
            row.AddChild(new Label { Text = label }); row.AddChild(id);
            foreach (var pair in new[] { ("x", x), ("y", y), ("w", width), ("h", height) }) { row.AddChild(new Label { Text = pair.Item1 }); row.AddChild(pair.Item2); }
            var remove = new Button { Text = "Remove" }; remove.Pressed += () => RemoveBox(frameIndex, hitbox, boxIndex); row.AddChild(remove);
            host.AddChild(row);
            var error = ErrorLabel(); host.AddChild(error);
            rows.Add(new BoxRow(id, x, y, width, height, error));
        }
    }

    private void AddCollectionHeader(string title, string buttonText, Action action)
    {
        var row = new HBoxContainer();
        var label = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var button = new Button { Text = buttonText }; button.Pressed += action;
        row.AddChild(label); row.AddChild(button); _form.AddChild(row);
    }

    private int FindMoveIndex(string? id) => id is null || _viewModel is null ? -1 : _viewModel.CurrentCandidate.Moves.ToList().FindIndex(move => string.Equals(move.MoveId, id, StringComparison.Ordinal));
    private void AddField(string name) { _form.AddChild(new Label { Text = name }); var edit = new LineEdit { PlaceholderText = name, TooltipText = name }; _fields.Add(name, edit); _form.AddChild(edit); var error = ErrorLabel(); _fieldErrors.Add(name, error); _form.AddChild(error); }
    private static SpinBox Number(double value, double min, double max) => new() { Value = value, MinValue = min, MaxValue = max, Step = 1, AllowGreater = true, AllowLesser = true, CustomMinimumSize = new Vector2(72, 0) };
    private void ClearNestedRows() { ClearChildren(_cancelRowsHost); ClearChildren(_collisionRowsHost); _cancelRows.Clear(); _collisionRows.Clear(); }
    private static void ClearChildren(Node node) { foreach (Node child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
    private static CollisionFrameDefinition CloneFrame(CollisionFrameDefinition source, IReadOnlyList<CollisionBoxDefinition>? hitboxes = null, IReadOnlyList<CollisionBoxDefinition>? hurtboxes = null) => new() { Frame = source.Frame, Hitboxes = hitboxes ?? source.Hitboxes, Hurtboxes = hurtboxes ?? source.Hurtboxes };

    private void ApplyValidationPresentation(MoveValidationResult result, string prefix)
    {
        ClearValidationPresentation();
        MoveValidationPresentation presentation = MoveValidationPresentation.From(result);
        var lines = new List<string> { $"{prefix} {presentation.Summary}." };
        int selectedMove = FindMoveIndex(_moveId);
        string? firstRelative = presentation.FirstFieldKey;
        if (presentation.FirstFieldKey is not null &&
            TryReadMovePath(presentation.FirstFieldKey, out int firstMove, out string relative))
        {
            firstRelative = relative;
            if (firstMove != selectedMove && _viewModel is not null && firstMove < _viewModel.CurrentCandidate.Moves.Count)
            {
                _loading = true;
                _moveSelector.Select(firstMove);
                LoadMove(firstMove);
                _loading = false;
                selectedMove = firstMove;
            }
            else if (selectedMove >= 0)
            {
                _loading = true;
                _moveSelector.Select(selectedMove);
                _loading = false;
            }
        }
        foreach (var pair in presentation.ErrorsByFieldKey)
        {
            string text = string.Join(" ", pair.Value);
            lines.Add($"{pair.Key}: {text}");
            if (TryReadMovePath(pair.Key, out int moveIndex, out string fieldKey))
            {
                if (moveIndex == selectedMove) ApplyInlineError(fieldKey, text);
            }
            else
                ApplyInlineError(pair.Key, text);
        }
        _status.Text = string.Join("\n", lines);
        FindControl(firstRelative)?.GrabFocus();
    }

    private void ClearValidationPresentation()
    {
        foreach (Label error in _fieldErrors.Values) error.Text = string.Empty;
        foreach (CancelRow row in _cancelRows) row.Error.Text = string.Empty;
        foreach (CollisionRow row in _collisionRows)
        {
            row.Error.Text = string.Empty;
            foreach (BoxRow box in row.Hitboxes.Concat(row.Hurtboxes)) box.Error.Text = string.Empty;
        }
    }

    private void ApplyInlineError(string key, string text)
    {
        if (_fieldErrors.TryGetValue(key, out Label? scalar)) { AppendError(scalar, text); return; }
        if (TryReadIndex(key, "cancel_windows", out int cancelIndex, out _))
        {
            if (cancelIndex < _cancelRows.Count) AppendError(_cancelRows[cancelIndex].Error, text);
            return;
        }
        if (!TryReadIndex(key, "collision_frames", out int frameIndex, out string remainder) || frameIndex >= _collisionRows.Count) return;
        CollisionRow frame = _collisionRows[frameIndex];
        if (TryReadIndex(remainder, "hitboxes", out int hitboxIndex, out _) && hitboxIndex < frame.Hitboxes.Count)
            AppendError(frame.Hitboxes[hitboxIndex].Error, text);
        else if (TryReadIndex(remainder, "hurtboxes", out int hurtboxIndex, out _) && hurtboxIndex < frame.Hurtboxes.Count)
            AppendError(frame.Hurtboxes[hurtboxIndex].Error, text);
        else
            AppendError(frame.Error, text);
    }

    private Control? FindControl(string? key)
    {
        if (key is null) return null;
        if (_fields.TryGetValue(key, out LineEdit? scalar)) return scalar;
        if (TryReadIndex(key, "cancel_windows", out int cancelIndex, out string cancelField) && cancelIndex < _cancelRows.Count)
        {
            CancelRow row = _cancelRows[cancelIndex];
            return cancelField switch { "start_frame" => row.Start, "end_frame" => row.End, _ => row.Category };
        }
        if (!TryReadIndex(key, "collision_frames", out int frameIndex, out string remainder) || frameIndex >= _collisionRows.Count) return null;
        CollisionRow frame = _collisionRows[frameIndex];
        if (remainder == "frame" || remainder.Length == 0) return frame.Frame;
        if (TryReadIndex(remainder, "hitboxes", out int hitboxIndex, out string hitboxField) && hitboxIndex < frame.Hitboxes.Count)
            return BoxControl(frame.Hitboxes[hitboxIndex], hitboxField);
        if (TryReadIndex(remainder, "hurtboxes", out int hurtboxIndex, out string hurtboxField) && hurtboxIndex < frame.Hurtboxes.Count)
            return BoxControl(frame.Hurtboxes[hurtboxIndex], hurtboxField);
        return frame.Frame;
    }

    private static Control BoxControl(BoxRow box, string field) => field switch
    {
        "x" => box.X, "y" => box.Y, "width" => box.Width, "height" => box.Height, _ => box.Id
    };

    private static bool TryReadIndex(string value, string collection, out int index, out string remainder)
    {
        index = -1; remainder = string.Empty;
        string prefix = $"{collection}[";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        int close = value.IndexOf(']', prefix.Length);
        if (close < 0 || !int.TryParse(value[prefix.Length..close], out index)) return false;
        remainder = close + 1 < value.Length && value[close + 1] == '.' ? value[(close + 2)..] : string.Empty;
        return true;
    }

    private static bool TryReadMovePath(string value, out int moveIndex, out string relative)
    {
        moveIndex = -1; relative = value;
        if (!TryReadIndex(value, "moves", out moveIndex, out relative)) return false;
        return true;
    }

    private static void AppendError(Label label, string text) => label.Text = string.IsNullOrEmpty(label.Text) ? text : $"{label.Text}\n{text}";
    private static Label ErrorLabel() => new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    private sealed record CancelRow(SpinBox Start, SpinBox End, LineEdit Category, Label Error);
    private sealed record BoxRow(LineEdit Id, SpinBox X, SpinBox Y, SpinBox Width, SpinBox Height, Label Error);
    private sealed record CollisionRow(SpinBox Frame, IReadOnlyList<BoxRow> Hitboxes, IReadOnlyList<BoxRow> Hurtboxes, Label Error);
}
#endif
