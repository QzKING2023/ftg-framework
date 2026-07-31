#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Data;
using FTG_Framework.Engine.Physics;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class PhysicsAllocationTests
{
    [Fact]
    public void Update_TwoFullSets_AllocatesZeroAfterWarmup()
    {
        var boxes = Boxes("h", 8, 0);
        var move = new MoveDefinition
        {
            MoveId = "perf", Active = 1,
            CollisionFrames = new[]
            {
                new CollisionFrameDefinition
                    { Frame = 1, Hitboxes = boxes, Hurtboxes = boxes }
            }
        };
        var store = new DataStore(new[] { move });
        var frames = new Frames();
        var engine = new PhysicsEngine(store, frames);
        engine.Initialize(store);
        engine.Register(new Participant(1, 0, boxes));
        engine.Register(new Participant(2, 10000, boxes));

        for (int i = 0; i < 100; i++) engine.Update();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 3600; i++) engine.Update();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static CollisionBoxDefinition[] Boxes(string prefix, int count, float x)
    {
        var result = new CollisionBoxDefinition[count];
        for (int i = 0; i < count; i++)
            result[i] = new CollisionBoxDefinition
            {
                BoxId = $"{prefix}{i}", X = x + i * 3, Width = 2, Height = 2
            };
        return result;
    }

    private sealed class Frames : IFrameDataEngine
    {
        public EvaluatedMoveFrame GetLastEvaluatedFrame(int id) =>
            new("perf", 1, 0, MovePhase.Active);
        public void StartMove(int p, string m) { }
        public void Update() { }
        public MovePhase GetPhase(int p) => GetLastEvaluatedFrame(p).Phase;
        public int GetCurrentFrame(int p) => 0;
        public string? GetCurrentMoveId(int p) => "perf";
        public void InterruptAndStart(int p, string m) { }
        public bool RestoreFrame(int f) => false;
        public int EarliestSnapshotFrame => -1;
        public FrameStateSnapshot? TryGetSnapshot(int f) => null;
        public void RestoreFromReplaySnapshot(FrameStateSnapshot s) { }
    }

    private sealed class Participant : IPhysicsParticipant
    {
        private readonly PhysicsParticipantSnapshot _snapshot;
        public Participant(int id, float x, IReadOnlyList<CollisionBoxDefinition> hurtboxes) =>
            _snapshot = new(id, $"p{id}", x, 0, DirectionValue.Neutral, true, hurtboxes);
        public int PlayerId => _snapshot.PlayerId;
        public PhysicsParticipantSnapshot CapturePhysicsSnapshot() => _snapshot;
    }
}
