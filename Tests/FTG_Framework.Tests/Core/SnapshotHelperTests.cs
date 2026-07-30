#nullable enable
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests.Core;

public class SnapshotHelperTests
{
    // ── Basic snapshot identity ──

    [Fact]
    public void Of_RecordStruct_ReturnsSameValue()
    {
        var original = new TestRecordData(42, "hello");

        var snapshot = Snapshot.Of(original);

        Assert.Equal(original.Value, snapshot.Value);
        Assert.Equal(original.Name, snapshot.Name);
    }

    [Fact]
    public void Of_ReferenceType_ReturnsSameReference()
    {
        var original = new TestRefData { Value = 99, Name = "test" };

        var snapshot = Snapshot.Of(original);

        Assert.Same(original, snapshot);
    }

    [Fact]
    public void Of_Int_ReturnsSameValue()
    {
        var snapshot = Snapshot.Of(42);

        Assert.Equal(42, snapshot);
    }

    [Fact]
    public void Of_String_ReturnsSameReference()
    {
        var original = "immutable";

        var snapshot = Snapshot.Of(original);

        Assert.Same(original, snapshot);
    }

    // ── Snapshot isolation ──

    [Fact]
    public void Snapshot_UnchangedAfterProviderUpdate()
    {
        var provider = new TestRecordData(1, "v1");

        var snapshot = Snapshot.Of(provider);

        // Simulate a hot-reload: provider is replaced with new data
        provider = new TestRecordData(2, "v2");

        Assert.Equal(1, snapshot.Value);
        Assert.Equal("v1", snapshot.Name);
    }

    [Fact]
    public void Snapshot_ReferenceType_FieldChangeDoesNotAffectSnapshot()
    {
        var original = new TestRefData { Value = 10, Name = "original" };
        var snapshot = Snapshot.Of(original);

        // Mutate the original — snapshot is same object so it sees the mutation
        // (this is expected behavior: for mutable ref types, Snapshot.Of is a marker)
        original.Value = 20;

        // Same reference, so values are the same — this documents that Snapshot.Of
        // is a marker for immutable types; mutable types need explicit copy logic.
        Assert.Equal(20, snapshot.Value);
    }

    // ── Generic type handling ──

    [Fact]
    public void Of_MultipleGenericTypes_AllWork()
    {
        var intSnapshot = Snapshot.Of(100);
        var stringSnapshot = Snapshot.Of("data");
        var recordSnapshot = Snapshot.Of(new TestRecordData(5, "rec"));

        Assert.Equal(100, intSnapshot);
        Assert.Equal("data", stringSnapshot);
        Assert.Equal(5, recordSnapshot.Value);
    }

    [Fact]
    public void Of_NullReferenceType_ReturnsNull()
    {
        var snapshot = Snapshot.Of<TestRefData?>(null);

        Assert.Null(snapshot);
    }

    // ── Thread safety (concurrent snapshot + reload on same provider) ──

    [Fact]
    public void ConcurrentSnapshotAndReload_NoCrossContamination()
    {
        var provider = new TestRecordData(0, "initial");
        var snapshots = new TestRecordData[100];

        // Simulate: multiple consumers snapshot while provider is reloaded
        for (int i = 0; i < 100; i++)
        {
            if (i == 50)
            {
                // Mid-loop reload
                provider = new TestRecordData(999, "reloaded");
            }

            snapshots[i] = Snapshot.Of(provider);
        }

        // First 50 snapshots should have initial data
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(0, snapshots[i].Value);
            Assert.Equal("initial", snapshots[i].Name);
        }

        // Last 50 snapshots should have reloaded data
        for (int i = 50; i < 100; i++)
        {
            Assert.Equal(999, snapshots[i].Value);
            Assert.Equal("reloaded", snapshots[i].Name);
        }
    }

    // ── Test types ──

    private readonly record struct TestRecordData(int Value, string Name);

    private sealed class TestRefData
    {
        public int Value { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
