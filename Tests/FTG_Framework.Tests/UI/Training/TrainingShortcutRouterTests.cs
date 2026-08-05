#nullable enable
using System;
using FTG_Framework.UI.Training;
using Xunit;

namespace FTG_Framework.Tests.UI.Training;

public sealed class TrainingShortcutRouterTests
{
    [Theory]
    [InlineData(TrainingShortcutRouter.RecordToggleAction, TrainingShortcutCommand.RecordToggle)]
    [InlineData(TrainingShortcutRouter.PlayOnceAction, TrainingShortcutCommand.PlayOnce)]
    [InlineData(TrainingShortcutRouter.LoopToggleAction, TrainingShortcutCommand.LoopToggle)]
    [InlineData(TrainingShortcutRouter.StopPlaybackAction, TrainingShortcutCommand.StopPlayback)]
    public void TryResolveCommand_MapsEveryNamedAction(
        string action, TrainingShortcutCommand expected)
    {
        Assert.True(TrainingShortcutRouter.TryResolveCommand(action, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryResolveCommand_RejectsGameplayAndUnknownActions()
    {
        Assert.False(TrainingShortcutRouter.TryResolveCommand("p1_light", out _));
        Assert.False(TrainingShortcutRouter.TryResolveCommand("", out _));
    }

    [Fact]
    public void TryResolveSingleCommand_RejectsConflictingTrainingBindingsWithoutDispatch()
    {
        Assert.False(TrainingShortcutRouter.TryResolveSingleCommand(new[]
        {
            TrainingShortcutRouter.RecordToggleAction,
            TrainingShortcutRouter.LoopToggleAction
        }, out _, out string conflict));
        Assert.Contains(TrainingShortcutRouter.RecordToggleAction, conflict);
        Assert.Contains(TrainingShortcutRouter.LoopToggleAction, conflict);
    }

    [Fact]
    public void TryResolveSingleCommand_DeduplicatesOneActionAndRejectsNonTrainingInput()
    {
        Assert.True(TrainingShortcutRouter.TryResolveSingleCommand(new[]
        {
            TrainingShortcutRouter.PlayOnceAction,
            TrainingShortcutRouter.PlayOnceAction
        }, out TrainingShortcutCommand command, out string conflict));
        Assert.Equal(TrainingShortcutCommand.PlayOnce, command);
        Assert.Empty(conflict);

        Assert.False(TrainingShortcutRouter.TryResolveSingleCommand(
            new[] { "p1_light" }, out _, out string unavailable));
        Assert.Contains("unavailable", unavailable);
    }

    [Fact]
    public void DispatchMatchedActions_ExecutesExactlyOnceOrRejectsConflictWithoutExecution()
    {
        int executeCount = 0;
        int rejectCount = 0;
        TrainingShortcutCommand executed = default;
        Assert.True(TrainingShortcutRouter.DispatchMatchedActions(
            new[] { TrainingShortcutRouter.StopPlaybackAction },
            command => { executeCount++; executed = command; },
            _ => rejectCount++));
        Assert.Equal(1, executeCount);
        Assert.Equal(0, rejectCount);
        Assert.Equal(TrainingShortcutCommand.StopPlayback, executed);

        Assert.True(TrainingShortcutRouter.DispatchMatchedActions(new[]
        {
            TrainingShortcutRouter.RecordToggleAction,
            TrainingShortcutRouter.LoopToggleAction
        }, _ => executeCount++, _ => rejectCount++));
        Assert.Equal(1, executeCount);
        Assert.Equal(1, rejectCount);
        Assert.False(TrainingShortcutRouter.DispatchMatchedActions(
            Array.Empty<string>(), _ => executeCount++, _ => rejectCount++));
    }
}
