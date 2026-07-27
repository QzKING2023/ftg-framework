#nullable enable
using System;
using Godot;

namespace FTG_Framework.Core;

/// <summary>
/// Pre-allocated, recycled object pool for Godot <see cref="Node"/> instances.
/// Uses <see cref="PoolCore{T}"/> for pool state management; this wrapper adds
/// scene-tree attachment and removal.
/// </summary>
/// <typeparam name="T">Godot Node type; must implement <see cref="IPoolable"/>.</typeparam>
public sealed class Pool<T> : IDisposable where T : Node, IPoolable
{
    private readonly PoolCore<T> _core;

    public int Capacity => _core.Capacity;
    public int MaxCapacity => _core.MaxCapacity;
    public int AvailableCount => _core.AvailableCount;
    public int AcquiredCount => _core.AcquiredCount;

    public Pool(int capacity, Func<T> factory, bool autoExpand = false, int? maxCapacity = null)
    {
        _core = new PoolCore<T>(capacity, factory, autoExpand, maxCapacity);
    }

    public T Acquire(Node parent)
    {
        if (parent is null)
            throw new ArgumentNullException(nameof(parent));

        T obj = _core.Acquire();
        try
        {
            parent.AddChild(obj);
        }
        catch
        {
            _core.Release(obj);
            throw;
        }
        return obj;
    }

    public void Release(T obj)
    {
        obj.GetParent()?.RemoveChild(obj);
        _core.Release(obj);
    }

    public void Dispose()
    {
        // PoolCore doesn't expose its internal collections,
        // so we free nodes by draining the pool.
        while (_core.AvailableCount > 0)
        {
            var obj = _core.Acquire();
            obj.QueueFree();
        }
    }
}
