namespace AngleSharp.Common;

using System;
using System.Buffers;

/// <summary>
/// A buffer that uses an array pool to store the characters.
/// Usage of this buffer assumes that the max capacity is known upfront
/// Maintains an append only contiguous chunk of characters.
/// Created <see cref="StringOrMemory"/> instances lifetime is tied to the buffer.
/// Works under assumption that the buffer will be disposed after use
/// and max capacity can't be larger than char count in the source.
/// </summary>
internal sealed class ArrayPoolBuffer : IMutableCharBuffer
{
    private Char[] _buffer;
    private Int32 _start;
    private Int32 _idx;
    private Boolean _disposed;

    private Int32 Pointer => _start + _idx;

    public ArrayPoolBuffer(Int32 length)
    {
        _buffer = ArrayPool<Char>.Shared.Rent(length);
    }

    public IMutableCharBuffer Append(Char c)
    {
        AppendFast(c);
        return this;
    }

    internal void AppendFast(Char c)
    {
        _buffer[_start + _idx] = c;
        _idx++;
    }

    public void Discard()
    {
        Clear(false);
    }

    private void Clear(Boolean commit)
    {
        if (commit)
        {
            _start += _idx;
        }
        _idx = 0;
    }

    public Int32 Length => _idx;

    public Int32 Capacity => _buffer.Length;

    public IMutableCharBuffer Remove(Int32 startIndex, Int32 length)
    {
        var source = _buffer.AsSpan(_start + startIndex + length, length);
        var destination = _buffer.AsSpan(_start + startIndex, length);
        source.CopyTo(destination);

        // Array.Copy(
        //     sourceArray: _buffer,
        //     sourceIndex: _start + startIndex + length,
        //     destinationArray: _buffer,
        //     destinationIndex: _start + startIndex,
        //     length: length);

        _idx -= length;
        return this;
    }

    public void ReturnToPool()
    {
        if (!_disposed)
        {
            ArrayPool<Char>.Shared.Return(_buffer, false);
            _buffer = null!;
            _disposed = true;
        }
    }

    public IMutableCharBuffer Insert(Int32 idx, Char c)
    {
        if ((UInt32)idx > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(idx));
        }

        if (Pointer + 1 > Capacity)
        {
            throw new InvalidOperationException("Buffer is full.");
        }

        Array.Copy(
            _buffer, _start + idx,
            _buffer, _start + idx + 1,
            Length - idx);

        _buffer[_start + idx] = c;
        _idx++;

        return this;
    }

    public IMutableCharBuffer Append(ReadOnlySpan<Char> str)
    {
        if (Pointer + str.Length > Capacity)
        {
            throw new InvalidOperationException("Buffer is full.");
        }

        str.CopyTo(_buffer.AsSpan(Pointer));
        _idx += str.Length;
        return this;
    }

    public Char this[Int32 i] => _buffer[_start + i];

    private StringOrMemory GetData() => new(_buffer.AsMemory(_start, Length));

    public StringOrMemory GetDataAndClear()
    {
        var result = GetData();
        Clear(true);
        return result;
    }

    public Boolean HasText(ReadOnlySpan<Char> test, StringComparison comparison = StringComparison.Ordinal)
    {
        var actual = _buffer.AsSpan(_start, Length);
        return MemoryExtensions.Equals(actual, test, comparison);
    }

    public Boolean HasTextAt(ReadOnlySpan<Char> test, Int32 offset, Int32 length, StringComparison comparison = StringComparison.Ordinal)
    {
        var actual = _buffer.AsSpan(_start + offset, length);
        return MemoryExtensions.Equals(actual, test, comparison);
    }

    String IMutableCharBuffer.ToString() => _buffer.AsMemory(_start, Length).ToString();

    public void Dispose()
    {
        ReturnToPool();
    }
}