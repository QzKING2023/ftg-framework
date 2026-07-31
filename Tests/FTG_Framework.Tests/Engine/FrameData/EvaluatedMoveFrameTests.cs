#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Data;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class EvaluatedMoveFrameTests
{
    [Fact]
    public void Update_ExposesPreTickFrameForPhysics()
    {
        var move = new MoveDefinition { MoveId = "5A", Startup = 1, Active = 1, Recovery = 1 };
        var store = new DataStore(new[] { move });
        var engine = new FrameDataEngine(store);

        engine.StartMove(1, "5A");
        engine.Update();

        var evaluated = engine.GetLastEvaluatedFrame(1);
        Assert.Equal(0, evaluated.TimelineFrame);
        Assert.Equal(1, evaluated.AuthoredFrame);
        Assert.Equal(MovePhase.Startup, evaluated.Phase);
        Assert.Equal(1, engine.GetCurrentFrame(1));
    }

    [Fact]
    public void NewMoveInstance_ChangesIdentity()
    {
        var move = new MoveDefinition { MoveId = "zero", Active = 1 };
        var store = new DataStore(new[] { move });
        var engine = new FrameDataEngine(store);
        engine.StartMove(1, "zero");
        engine.Update();
        long first = engine.GetLastEvaluatedFrame(1).MoveInstanceId;
        engine.StartMove(1, "zero");
        engine.Update();
        long second = engine.GetLastEvaluatedFrame(1).MoveInstanceId;
        Assert.True(second > first);
    }
}
