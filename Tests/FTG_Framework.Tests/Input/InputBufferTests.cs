#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public class InputBufferTests : System.IDisposable
{
    public void Dispose()
    {
        EventBus.Instance.ProcessFrame();
    }

    [Fact]
    public void TryMatch_DirectionAndButtonMatch_ReturnsResult()
    {
        int startFrame = EventBus.Instance.CurrentFrame;
        for (int i = 0; i < 10; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;
        int entryFrame = currentFrame - 2;

        var history = new StubInputHistory();
        history.AddButtonEntry(1, entryFrame, ButtonValue.C);
        var leniency = new StubInputLeniency
        {
            TryMatchResult = new List<MatchResult>
            {
                new("dp_c", ButtonValue.C, entryFrame, 3)
            }
        };
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6);

        var results = buffer.TryMatch(1);
        Assert.Single(results);
        Assert.Equal("dp_c", results[0].MoveId);
    }

    [Fact]
    public void TryMatch_DirectionMatchWithoutButton_ReturnsEmpty()
    {
        int startFrame = EventBus.Instance.CurrentFrame;
        for (int i = 0; i < 10; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;

        var history = new StubInputHistory();
        // No matching button in history
        var leniency = new StubInputLeniency
        {
            TryMatchResult = new List<MatchResult>
            {
                new("dp_c", ButtonValue.C, currentFrame - 2, 3)
            }
        };
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6);

        var results = buffer.TryMatch(1);
        Assert.Empty(results);
    }

    [Fact]
    public void TryMatch_ButtonOutsideBufferWindow_ReturnsEmpty()
    {
        int startFrame = EventBus.Instance.CurrentFrame;
        for (int i = 0; i < 20; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;

        var history = new StubInputHistory();
        // Button at startFrame (well outside 6-frame window from currentFrame)
        history.AddButtonEntry(1, startFrame, ButtonValue.C);
        var leniency = new StubInputLeniency
        {
            TryMatchResult = new List<MatchResult>
            {
                new("dp_c", ButtonValue.C, currentFrame - 2, 3)
            }
        };
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6);

        var results = buffer.TryMatch(1);
        Assert.Empty(results);
    }

    [Fact]
    public void TryMatch_MultipleDirectionMatches_AllReturned()
    {
        int startFrame = EventBus.Instance.CurrentFrame;
        for (int i = 0; i < 10; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;
        int entryFrame = currentFrame - 2;

        var history = new StubInputHistory();
        history.AddButtonEntry(1, entryFrame, ButtonValue.C);
        var leniency = new StubInputLeniency
        {
            TryMatchResult = new List<MatchResult>
            {
                new("dp_c", ButtonValue.C, entryFrame - 1, 3),
                new("fireball_c", ButtonValue.C, entryFrame, 3)
            }
        };
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6);

        var results = buffer.TryMatch(1);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void TryMatch_PassesMotionWindowToLeniency()
    {
        for (int i = 0; i < 40; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;

        var history = new StubInputHistory();
        var leniency = new StubInputLeniency();
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6, motionWindow: 30);

        buffer.TryMatch(1);

        Assert.Equal(currentFrame - 30, leniency.LastFromFrame);
        Assert.Equal(currentFrame, leniency.LastToFrame);
    }

    [Fact]
    public void TryMatch_MotionOutsideBufferWindowButWithinMotionWindow_ReturnsResult()
    {
        for (int i = 0; i < 40; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;

        var history = new StubInputHistory();
        history.AddButtonEntry(1, currentFrame - 2, ButtonValue.C);
        var leniency = new StubInputLeniency
        {
            TryMatchResult = new List<MatchResult>
            {
                new("dp_c", ButtonValue.C, currentFrame - 20, 3)
            }
        };
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6, motionWindow: 30);

        var results = buffer.TryMatch(1);
        Assert.Single(results);
        Assert.Equal("dp_c", results[0].MoveId);
    }

    [Fact]
    public void TryMatch_NeutralNormal_RequiresDirectionMatchOnCurrentFrame()
    {
        for (int i = 0; i < 10; i++) EventBus.Instance.ProcessFrame();
        int currentFrame = EventBus.Instance.CurrentFrame;
        var history = new StubInputHistory();
        history.AddButtonEntry(1, currentFrame, ButtonValue.A);
        var leniency = new StubInputLeniency
        {
            TryMatchResult = new List<MatchResult>
            {
                new("5LP", ButtonValue.A, currentFrame - 1, 1)
            }
        };
        var buffer = new InputBuffer(history, leniency, bufferDuration: 6);

        Assert.Empty(buffer.TryMatch(1));

        leniency.TryMatchResult = new List<MatchResult>
        {
            new("5LP", ButtonValue.A, currentFrame, 1),
            new("dp_c", ButtonValue.C, currentFrame - 5, 3)
        };
        history.AddButtonEntry(1, currentFrame, ButtonValue.C);
        var matches = buffer.TryMatch(1);
        Assert.Contains(matches, match => match.MoveId == "5LP");
        Assert.Contains(matches, match => match.MoveId == "dp_c");

        history = new StubInputHistory();
        history.AddButtonEntry(1, currentFrame - 1, ButtonValue.A);
        buffer = new InputBuffer(history, leniency, bufferDuration: 6);
        leniency.TryMatchResult = new List<MatchResult>
        {
            new("5LP", ButtonValue.A, currentFrame, 1)
        };
        Assert.Empty(buffer.TryMatch(1));
    }
}
