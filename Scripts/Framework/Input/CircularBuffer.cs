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
        // Oldest item sits _count slots behind _head — a plain `_count < len ? 0`
        // shortcut is wrong once the buffer has wrapped and then been tail-trimmed.
        int start = (_head - _count + _buffer.Length) % _buffer.Length;
        for (int i = 0; i < _count; i++)
            result[i] = _buffer[(start + i) % _buffer.Length];
        return result;
    }

    public void Replace(IEnumerable<T> items)
    {
        Array.Clear(_buffer);
        _head = 0;
        _count = 0;
        foreach (T item in items) Add(item);
    }

    // Drops trailing entries while the predicate holds. Entries are appended in
    // chronological order, so a frame-based predicate removes a contiguous suffix.
    public void RemoveTailWhile(Func<T, bool> predicate)
    {
        while (_count > 0)
        {
            int tailIndex = (_head - 1 + _buffer.Length) % _buffer.Length;
            if (!predicate(_buffer[tailIndex]))
                break;
            _head = tailIndex;
            _count--;
        }
    }
}
