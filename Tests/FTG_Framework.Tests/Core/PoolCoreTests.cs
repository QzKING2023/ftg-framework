#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests.Core;

public class PoolCoreTests
{
    private sealed class TestPoolable : IPoolable
    {
        public int ResetCallCount { get; private set; }
        public string Tag { get; set; } = string.Empty;

        public void Reset()
        {
            ResetCallCount++;
            Tag = string.Empty;
        }
    }

    // ── Constructor (AC 8, AC 1) ──

    [Fact]
    public void Constructor_CapacityLessThanOne_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new PoolCore<TestPoolable>(0, () => new TestPoolable()));
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PoolCore<TestPoolable>(5, null!));
    }

    [Fact]
    public void Constructor_FactoryReturnsNull_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PoolCore<TestPoolable>(2, () => null!));
    }

    [Fact]
    public void Constructor_MaxCapacityLessThanCapacity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new PoolCore<TestPoolable>(5, () => new TestPoolable(), maxCapacity: 3));
    }

    [Fact]
    public void Constructor_PreAllocatesExactlyCapacityInstances()
    {
        int factoryCalls = 0;
        var pool = new PoolCore<TestPoolable>(3, () => { factoryCalls++; return new TestPoolable(); });

        Assert.Equal(3, factoryCalls);
        Assert.Equal(3, pool.Capacity);
        Assert.Equal(3, pool.AvailableCount);
        Assert.Equal(0, pool.AcquiredCount);
    }

    [Fact]
    public void Constructor_MaxCapacityDefaultsToDoubleCapacity()
    {
        var pool = new PoolCore<TestPoolable>(5, () => new TestPoolable());

        Assert.Equal(10, pool.MaxCapacity);
    }

    [Fact]
    public void Constructor_ExplicitMaxCapacity_Respected()
    {
        var pool = new PoolCore<TestPoolable>(3, () => new TestPoolable(), maxCapacity: 8);

        Assert.Equal(8, pool.MaxCapacity);
    }

    // ── Acquire / Release lifecycle (AC 2) ──

    [Fact]
    public void Acquire_ReturnsPreAllocatedInstance()
    {
        var pool = new PoolCore<TestPoolable>(2, () => new TestPoolable());

        var obj = pool.Acquire();

        Assert.NotNull(obj);
        Assert.Equal(1, pool.AcquiredCount);
        Assert.Equal(1, pool.AvailableCount);
    }

    [Fact]
    public void Acquire_CallsResetBeforeReturning()
    {
        var pool = new PoolCore<TestPoolable>(1, () => new TestPoolable());

        var obj = pool.Acquire();

        Assert.Equal(1, obj.ResetCallCount);
    }

    [Fact]
    public void Acquire_CallsResetEveryTime()
    {
        var pool = new PoolCore<TestPoolable>(1, () => new TestPoolable());
        var obj = pool.Acquire();
        pool.Release(obj);
        obj.Tag = "dirty";

        var reacquired = pool.Acquire();

        Assert.Same(obj, reacquired);
        Assert.Equal(2, obj.ResetCallCount);
        Assert.Equal(string.Empty, obj.Tag);
    }

    [Fact]
    public void Release_ReturnsNodeToFreeList()
    {
        var pool = new PoolCore<TestPoolable>(1, () => new TestPoolable());
        var obj = pool.Acquire();

        pool.Release(obj);

        Assert.Equal(0, pool.AcquiredCount);
        Assert.Equal(1, pool.AvailableCount);
    }

    // ── Exhaustion (AC 5) ──

    [Fact]
    public void Acquire_WhenExhausted_ThrowsInvalidOperationException()
    {
        var pool = new PoolCore<TestPoolable>(2, () => new TestPoolable());
        pool.Acquire();
        pool.Acquire();

        var ex = Assert.Throws<InvalidOperationException>(() => pool.Acquire());
        Assert.Contains("[Core]", ex.Message);
        Assert.Contains("exhausted", ex.Message);
    }

    // ── Auto-Expand (AC 6) ──

    [Fact]
    public void Acquire_WithAutoExpand_GrowsPool()
    {
        var logMessages = new List<string>();
        FrameworkLog.Info = logMessages.Add;
        try
        {
            var pool = new PoolCore<TestPoolable>(2, () => new TestPoolable(), autoExpand: true);

            pool.Acquire();
            pool.Acquire();
            var obj3 = pool.Acquire();

            Assert.NotNull(obj3);
            Assert.Equal(3, pool.AcquiredCount);
            Assert.Equal(0, pool.AvailableCount);
            Assert.Contains(logMessages, m => m.Contains("Pool expanded"));
        }
        finally
        {
            FrameworkLog.Info = Console.WriteLine;
        }
    }

    [Fact]
    public void Acquire_WithAutoExpand_ExpandsBeyondMaxCapacity()
    {
        var errorMessages = new List<string>();
        FrameworkLog.Error = errorMessages.Add;
        try
        {
            var pool = new PoolCore<TestPoolable>(
                capacity: 1,
                factory: () => new TestPoolable(),
                autoExpand: true,
                maxCapacity: 2);

            pool.Acquire(); // capacity 1
            pool.Acquire(); // expand to 2 (max)
            var obj3 = pool.Acquire(); // expands beyond max

            Assert.NotNull(obj3);
            Assert.Equal(3, pool.AcquiredCount);
            Assert.Contains(errorMessages, m => m.Contains("exceeded max capacity"));
        }
        finally
        {
            FrameworkLog.Error = Console.Error.WriteLine;
        }
    }

    [Fact]
    public void Acquire_WithAutoExpand_FactoryReturnsNull_Throws()
    {
        int callCount = 0;
        var pool = new PoolCore<TestPoolable>(
            capacity: 1,
            factory: () =>
            {
                callCount++;
                return callCount <= 1 ? new TestPoolable() : null!;
            },
            autoExpand: true);

        pool.Acquire();
        Assert.Throws<InvalidOperationException>(() => pool.Acquire());
    }

    // ── Invalid Release Prevention (AC 7) ──

    [Fact]
    public void Release_Null_ThrowsArgumentNullException()
    {
        var pool = new PoolCore<TestPoolable>(1, () => new TestPoolable());

        Assert.Throws<ArgumentNullException>(() => pool.Release(null!));
    }

    [Fact]
    public void Release_NodeNotAcquired_ThrowsInvalidOperationException()
    {
        var pool = new PoolCore<TestPoolable>(1, () => new TestPoolable());
        var stranger = new TestPoolable();

        var ex = Assert.Throws<InvalidOperationException>(() => pool.Release(stranger));
        Assert.Contains("[Core]", ex.Message);
    }

    [Fact]
    public void Release_DoubleRelease_ThrowsInvalidOperationException()
    {
        var pool = new PoolCore<TestPoolable>(1, () => new TestPoolable());
        var obj = pool.Acquire();
        pool.Release(obj);

        var ex = Assert.Throws<InvalidOperationException>(() => pool.Release(obj));
        Assert.Contains("[Core]", ex.Message);
    }

    // ── Properties (AC 2, AC 9) ──

    [Fact]
    public void Properties_ReflectCurrentState()
    {
        var pool = new PoolCore<TestPoolable>(3, () => new TestPoolable());

        Assert.Equal(3, pool.Capacity);
        Assert.Equal(3, pool.AvailableCount);
        Assert.Equal(0, pool.AcquiredCount);

        var a = pool.Acquire();
        Assert.Equal(2, pool.AvailableCount);
        Assert.Equal(1, pool.AcquiredCount);

        pool.Release(a);
        Assert.Equal(3, pool.AvailableCount);
        Assert.Equal(0, pool.AcquiredCount);
    }

    // ── Full cycle stress (AC 3 partial — GC allocation test needs Godot) ──

    [Fact]
    public void AcquireRelease_FullCycle_NoStateLeaks()
    {
        var pool = new PoolCore<TestPoolable>(5, () => new TestPoolable());
        var acquiredList = new List<TestPoolable>();

        for (int cycle = 0; cycle < 10; cycle++)
        {
            for (int i = 0; i < 5; i++)
            {
                var obj = pool.Acquire();
                obj.Tag = $"cycle-{cycle}-slot-{i}";
                acquiredList.Add(obj);
            }

            Assert.Equal(5, pool.AcquiredCount);
            Assert.Equal(0, pool.AvailableCount);

            foreach (var obj in acquiredList)
            {
                pool.Release(obj);
            }
            acquiredList.Clear();

            Assert.Equal(0, pool.AcquiredCount);
            Assert.Equal(5, pool.AvailableCount);
        }
    }

    [Fact]
    public void Reset_OnAcquire_ClearsPreviousState()
    {
        var pool = new PoolCore<TestPoolable>(2, () => new TestPoolable());
        var obj1 = pool.Acquire();
        obj1.Tag = "used";
        pool.Release(obj1);

        var obj2 = pool.Acquire();

        Assert.Same(obj1, obj2);
        Assert.Equal(string.Empty, obj2.Tag);
        Assert.Equal(2, obj2.ResetCallCount);
    }
}
