#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Engine.FrameData;
using Xunit;

namespace FTG_Framework.Tests.Replay;

public class FrameDataEngineReplayTests : IDisposable
{
    public void Dispose()
    {
        EventBusTestHelper.Drain();
    }

    [Fact]
    public void RestoreFromReplaySnapshot_DoesNotPublishFrameRewoundEvent()
    {
        var stubData = new StubDataStore();
        var engine = new FrameDataEngine(stubData);
        engine.Initialize(stubData);

        bool rewoundPublished = false;
        void OnRewound(FrameRewoundEvent e) => rewoundPublished = true;
        EventBus.Instance.Subscribe<FrameRewoundEvent>(OnRewound);

        try
        {
            var snapshot = new FrameStateSnapshot(0, "5LP", 3, MovePhase.Startup, null, 0, MovePhase.Idle);
            engine.RestoreFromReplaySnapshot(snapshot);

            Assert.False(rewoundPublished, "RestoreFromReplaySnapshot should NOT publish FrameRewoundEvent");
        }
        finally
        {
            EventBus.Instance.Unsubscribe<FrameRewoundEvent>(OnRewound);
            engine.Shutdown();
        }
    }

    [Fact]
    public void RestoreFromReplaySnapshot_RestoresCorrectState()
    {
        var stubData = new StubDataStore();
        stubData.SetMove(new FTG_Framework.Data.MoveDefinition
        {
            MoveId = "5LP", Startup = 3, Active = 2, Recovery = 5,
            Damage = 30, HitAdvantage = 3, BlockAdvantage = -2
        });

        var engine = new FrameDataEngine(stubData);
        engine.Initialize(stubData);

        engine.StartMove(1, "5LP");
        engine.Update();

        var snapshot = engine.TryGetSnapshot(EventBus.Instance.CurrentFrame);
        Assert.NotNull(snapshot);
        Assert.Equal("5LP", snapshot!.Value.P1MoveId);

        var engine2 = new FrameDataEngine(stubData);
        engine2.Initialize(stubData);
        engine2.RestoreFromReplaySnapshot(snapshot.Value);

        Assert.Equal("5LP", engine2.GetCurrentMoveId(1));

        engine.Shutdown();
        engine2.Shutdown();
    }
}
