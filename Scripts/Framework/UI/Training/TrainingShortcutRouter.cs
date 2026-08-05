#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FTG_Framework.UI.Training;

public enum TrainingShortcutCommand
{
    RecordToggle,
    PlayOnce,
    LoopToggle,
    StopPlayback
}

public partial class TrainingShortcutRouter : Node
{
    public const string RecordToggleAction = "training_record_toggle";
    public const string PlayOnceAction = "training_play_once";
    public const string LoopToggleAction = "training_loop_toggle";
    public const string StopPlaybackAction = "training_playback_stop";

    private static readonly IReadOnlyDictionary<string, TrainingShortcutCommand> Commands =
        new Dictionary<string, TrainingShortcutCommand>(StringComparer.Ordinal)
        {
            [RecordToggleAction] = TrainingShortcutCommand.RecordToggle,
            [PlayOnceAction] = TrainingShortcutCommand.PlayOnce,
            [LoopToggleAction] = TrainingShortcutCommand.LoopToggle,
            [StopPlaybackAction] = TrainingShortcutCommand.StopPlayback
        };

    public TrainingInputPlaybackPanel? Panel { get; set; }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsTextEditing(GetViewport().GuiGetFocusOwner())) return;
        string[] matches = Commands.Keys
            .Where(action => @event.IsActionPressed(action, allowEcho: false, exactMatch: true))
            .ToArray();
        if (!DispatchMatchedActions(
                matches,
                command => Panel?.ExecuteShortcut(command),
                conflict => Panel?.ShowShortcutConflict(conflict))) return;
        GetViewport().SetInputAsHandled();
    }

    internal static bool TryResolveCommand(string action, out TrainingShortcutCommand command) =>
        Commands.TryGetValue(action, out command);

    internal static bool TryResolveSingleCommand(
        IEnumerable<string> actions,
        out TrainingShortcutCommand command,
        out string conflict)
    {
        string[] matches = actions.Where(Commands.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
        if (matches.Length == 1)
        {
            command = Commands[matches[0]];
            conflict = string.Empty;
            return true;
        }
        command = default;
        conflict = matches.Length == 0
            ? "Training shortcut is unavailable."
            : $"Training shortcut conflict: {string.Join(", ", matches)}";
        return false;
    }

    internal static bool DispatchMatchedActions(
        IEnumerable<string> actions,
        Action<TrainingShortcutCommand> execute,
        Action<string> reject)
    {
        string[] matches = actions.ToArray();
        if (matches.Length == 0) return false;
        if (TryResolveSingleCommand(matches, out TrainingShortcutCommand command, out string conflict))
            execute(command);
        else
            reject(conflict);
        return true;
    }

    internal static bool IsTextEditing(Control? focusOwner) =>
        focusOwner is LineEdit or TextEdit or CodeEdit;

    internal static string DescribeBindings(string action)
    {
        if (!InputMap.HasAction(action)) return "Unbound";
        string[] bindings = InputMap.ActionGetEvents(action)
            .Select(input => input.AsText())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return bindings.Length == 0 ? "Unbound" : string.Join(" / ", bindings);
    }
}
