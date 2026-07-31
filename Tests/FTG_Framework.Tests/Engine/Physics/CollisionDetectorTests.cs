#nullable enable
using FTG_Framework.Data;
using FTG_Framework.Engine.Physics;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class CollisionDetectorTests
{
    [Fact]
    public void Overlaps_UsesHalfOpenEdges()
    {
        var a = new WorldCollisionBox("a", 0, 0, 10, 10);
        var touching = new WorldCollisionBox("b", 10, 0, 10, 10);
        var overlapping = new WorldCollisionBox("b", 9.99f, 0, 10, 10);

        Assert.False(CollisionDetector.Overlaps(a, touching));
        Assert.True(CollisionDetector.Overlaps(a, overlapping));
    }

    [Fact]
    public void ToWorld_MirrorsOnlyCenterOffset()
    {
        var box = new CollisionBoxDefinition { BoxId = "x", X = 3, Y = 2, Width = 4, Height = 6 };
        Assert.Equal(13, CollisionDetector.ToWorld(box, 10, 20, facingRight: true).CenterX);
        Assert.Equal(7, CollisionDetector.ToWorld(box, 10, 20, facingRight: false).CenterX);
    }

    [Fact]
    public void ZeroArea_NeverOverlaps()
    {
        var zero = new WorldCollisionBox("z", 0, 0, 0, 10);
        var normal = new WorldCollisionBox("n", 0, 0, 10, 10);
        Assert.False(CollisionDetector.Overlaps(zero, normal));
    }
}
