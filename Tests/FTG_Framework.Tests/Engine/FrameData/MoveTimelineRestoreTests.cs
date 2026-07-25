#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests;

public class MoveTimelineRestoreTests
{
    private static Data.DataStore MakeDataStore()
    {
        var move = new Data.MoveDefinition
        {
            MoveId = "5LP",
            Startup = 3,
            Active = 2,
            Recovery = 4,
            HitAdvantage = 2,
            BlockAdvantage = -3,
            Damage = 30
        };
        return new Data.DataStore(new[] { move });
    }

    [Fact]
    public void Restore_WithValidMoveId_RestoresCorrectPhaseAndFrame()
    {
        var dataStore = MakeDataStore();
        var timeline = new MoveTimeline { DataStore = dataStore };

        timeline.Restore("5LP", 2, MovePhase.Startup);

        Assert.Equal(MovePhase.Startup, timeline.Phase);
        Assert.Equal(2, timeline.CurrentFrame);
        Assert.Equal("5LP", timeline.MoveId);
        Assert.True(timeline.TotalFrames > 0);
    }

    [Fact]
    public void Restore_WithNullMoveId_ResetsToIdle()
    {
        var timeline = new MoveTimeline();

        timeline.Restore(null, 0, MovePhase.Idle);

        Assert.Equal(MovePhase.Idle, timeline.Phase);
        Assert.Equal(0, timeline.CurrentFrame);
        Assert.Null(timeline.MoveId);
    }

    [Fact]
    public void Restore_WithUnknownMoveId_ResetsToIdle()
    {
        var dataStore = MakeDataStore();
        var timeline = new MoveTimeline { DataStore = dataStore };

        timeline.Restore("nonexistent_move", 5, MovePhase.Active);

        Assert.Equal(MovePhase.Idle, timeline.Phase);
    }
}
