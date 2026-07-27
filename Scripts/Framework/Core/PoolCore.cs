#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Core;

/// <summary>
/// Pure-C# object pool state machine with no Godot dependency.
/// Handles free-list management, capacity enforcement, Reset invocation,
/// and auto-expand logic. Scene-tree operations live in <see cref="Pool{T}"/>.
/// </summary>
/// <typeparam name="T">Pooled object type; must implement <see cref="IPoolable"/>.</typeparam>
internal sealed class PoolCore<T> where T : class, IPoolable
{
    private readonly Stack<T> _freeList;
    private readonly HashSet<T> _acquired;
    private readonly Func<T> _factory;
    private readonly int _capacity;
    private readonly int _maxCapacity;
    private readonly bool _autoExpand;

    public int Capacity => _capacity;
    public int MaxCapacity => _maxCapacity;
    public int AvailableCount => _freeList.Count;
    public int AcquiredCount => _acquired.Count;

    public PoolCore(int capacity, Func<T> factory, bool autoExpand = false, int? maxCapacity = null)
    {
        if (capacity < 1)
            throw new ArgumentException("[Core] Pool capacity must be >= 1.", nameof(capacity));

        _capacity = capacity;
        _maxCapacity = maxCapacity ?? (capacity > int.MaxValue / 2 ? int.MaxValue : capacity * 2);

        if (_maxCapacity < capacity)
            throw new ArgumentException("[Core] maxCapacity must be >= capacity.", nameof(maxCapacity));

        _autoExpand = autoExpand;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _freeList = new Stack<T>(capacity);
        _acquired = new HashSet<T>();

        for (int i = 0; i < capacity; i++)
        {
            var created = factory();
            if (created is null)
                throw new InvalidOperationException("[Core] Factory returned null during pre-allocation.");
            _freeList.Push(created);
        }
    }

    public T Acquire()
    {
        if (!_freeList.TryPop(out T? obj))
        {
            if (_autoExpand)
            {
                obj = _factory();
                if (obj is null)
                    throw new InvalidOperationException("[Core] Factory returned null during auto-expand.");

                if (_acquired.Count < _maxCapacity)
                {
                    FrameworkLog.Info("[Core] Pool expanded: new instance allocated.");
                }
                else
                {
                    FrameworkLog.Error("[Core] Pool exceeded max capacity; expanding beyond limit.");
                }
            }
            else
            {
                throw new InvalidOperationException($"[Core] Pool exhausted: capacity={_capacity}");
            }
        }

        obj.Reset();
        _acquired.Add(obj);
        return obj;
    }

    public void Release(T obj)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj));

        if (!_acquired.Remove(obj))
            throw new InvalidOperationException("[Core] Cannot release node not acquired from this pool.");

        _freeList.Push(obj);
    }
}
