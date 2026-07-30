#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Input;

public class DefaultInputPipelineTests : IDisposable
{
    private readonly InputHistory _history = new(60);
    private readonly InputLeniencyMatcher _matcher;
    private readonly InputBuffer _buffer;
    private readonly DefaultPriorityResolver _resolver;

    public DefaultInputPipelineTests()
    {
        EventBusTestHelper.Drain();
        _matcher = new InputLeniencyMatcher(_history);
        GameLoop.RegisterDefaultMoves(_matcher);
        _buffer = new InputBuffer(_history, _matcher, bufferDuration: 6, motionWindow: 30);
        _resolver = new DefaultPriorityResolver(_matcher, new ChargeTracker(_history));
    }

    public void Dispose()
    {
        _history.Shutdown();
        EventBusTestHelper.Drain();
    }

    [Theory]
    [InlineData(ButtonValue.A, "5LP")]
    [InlineData(ButtonValue.B, "5HP")]
    public void RepeatedNeutral_CurrentNeutralAndButton_ResolvesNormal(
        ButtonValue button, string expectedMove)
    {
        RecordFrame(DirectionValue.Neutral);
        RecordFrame(DirectionValue.Neutral);
        _history.RecordInput(1, InputType.Directional, (int)DirectionValue.Neutral);
        _history.RecordInput(1, InputType.Button, (int)button);

        var resolved = _resolver.Resolve(
            _buffer.TryMatch(1), 1, EventBus.Instance.CurrentFrame);

        Assert.NotNull(resolved);
        Assert.Equal(expectedMove, resolved.Value.MoveId);
    }

    [Fact]
    public void StaleNeutral_CurrentNonNeutralAndButton_DoesNotResolveNormal()
    {
        RecordFrame(DirectionValue.Neutral);
        _history.RecordInput(1, InputType.Directional, (int)DirectionValue.Forward);
        _history.RecordInput(1, InputType.Button, (int)ButtonValue.A);

        Assert.DoesNotContain(_buffer.TryMatch(1), match => match.MoveId == "5LP");
    }

    [Fact]
    public void ExistingSpecialMotion_StillResolves()
    {
        RecordFrame(DirectionValue.Down);
        RecordFrame(DirectionValue.DownForward);
        _history.RecordInput(1, InputType.Directional, (int)DirectionValue.Forward);
        _history.RecordInput(1, InputType.Button, (int)ButtonValue.C);

        var resolved = _resolver.Resolve(
            _buffer.TryMatch(1), 1, EventBus.Instance.CurrentFrame);

        Assert.NotNull(resolved);
        Assert.Equal("fireball_c", resolved.Value.MoveId);
    }

    [Theory]
    [InlineData(ButtonValue.C, "dp_c")]
    [InlineData(ButtonValue.D, "dp_d")]
    public void ExistingDpMotion_StillResolves(
        ButtonValue button, string expectedMove)
    {
        RecordFrame(DirectionValue.Forward);
        RecordFrame(DirectionValue.Down);
        _history.RecordInput(1, InputType.Directional, (int)DirectionValue.DownForward);
        _history.RecordInput(1, InputType.Button, (int)button);

        var resolved = _resolver.Resolve(
            _buffer.TryMatch(1), 1, EventBus.Instance.CurrentFrame);

        Assert.NotNull(resolved);
        Assert.Equal(expectedMove, resolved.Value.MoveId);
    }

    [Fact]
    public void OverlappingDpAndFireball_PreservesRegisteredPriority()
    {
        RecordFrame(DirectionValue.Forward);
        RecordFrame(DirectionValue.Down);
        RecordFrame(DirectionValue.DownForward);
        _history.RecordInput(1, InputType.Directional, (int)DirectionValue.Forward);
        _history.RecordInput(1, InputType.Button, (int)ButtonValue.C);

        var resolved = _resolver.Resolve(
            _buffer.TryMatch(1), 1, EventBus.Instance.CurrentFrame);

        Assert.NotNull(resolved);
        Assert.Equal("dp_c", resolved.Value.MoveId);
    }

    private void RecordFrame(DirectionValue direction)
    {
        _history.RecordInput(1, InputType.Directional, (int)direction);
        EventBus.Instance.ProcessFrame();
    }
}
