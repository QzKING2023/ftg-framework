#nullable enable
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests.Input;

public class CircularBufferTests
{
    [Fact]
    public void Snapshot_BeforeWrap_ReturnsInsertionOrder()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        Assert.Equal(new[] { 1, 2, 3 }, buffer.Snapshot());
    }

    [Fact]
    public void Snapshot_AfterWrap_ReturnsChronologicalOrder()
    {
        var buffer = new CircularBuffer<int>(3);
        for (int i = 1; i <= 5; i++)
            buffer.Add(i);

        Assert.Equal(new[] { 3, 4, 5 }, buffer.Snapshot());
    }

    // Regression: after the buffer has wrapped and then been tail-trimmed (the
    // rewind path), the not-full shortcut used to start at slot 0 — resurrecting
    // the trimmed entry and dropping the newest one.
    [Fact]
    public void Snapshot_AfterWrapAndTailTrim_DoesNotResurrectTrimmedEntries()
    {
        var buffer = new CircularBuffer<int>(5);
        for (int i = 1; i <= 6; i++)
            buffer.Add(i); // wraps: logical contents 2,3,4,5,6

        buffer.RemoveTailWhile(x => x == 6);

        Assert.Equal(new[] { 2, 3, 4, 5 }, buffer.Snapshot());
    }

    [Fact]
    public void RemoveTailWhile_ToEmpty_SnapshotStaysConsistent()
    {
        var buffer = new CircularBuffer<int>(3);
        for (int i = 1; i <= 5; i++)
            buffer.Add(i);

        buffer.RemoveTailWhile(_ => true);

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());

        buffer.Add(9);
        Assert.Equal(new[] { 9 }, buffer.Snapshot());
    }
}
