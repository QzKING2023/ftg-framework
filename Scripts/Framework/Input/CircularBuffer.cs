#nullable enable
using System;
using System.Collections.Generic;

namespace FTG_Framework.Input;

internal sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException($"[Input] CircularBuffer capacity must be > 0, got {capacity}");
        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
            _count++;
    }

    public IReadOnlyList<T> Snapshot()
    {
        if (_count == 0)
            return Array.Empty<T>();
        var result = new T[_count];
        int start = _count < _buffer.Length ? 0 : _head;
        for (int i = 0; i < _count; i++)
            result[i] = _buffer[(start + i) % _buffer.Length];
        return result;
    }
}
