//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

/// <summary>
/// A stream that returns Zero's.
/// </summary>
public class ZeroStream : MappedStream
{
    private bool _atEof;
    private readonly long _length;
    private long _position;

    public ZeroStream(long length)
    {
        _length = length;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override IEnumerable<StreamExtent> Extents
        // The stream is entirely sparse
        => new List<StreamExtent>(0);

    public override long Length => _length;

    public override long Position
    {
        get => _position;

        set
        {
            _position = value;
            _atEof = false;
        }
    }

    public override IEnumerable<StreamExtent> MapContent(long start, long length)
    {
        return [];
    }

    public override void Flush() {}

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == _length)
        {
            if (_atEof)
            {
                throw new IOException("Attempt to read beyond end of stream");
            }

            _atEof = true;
            return 0;
        }

        var numToClear = (int)Math.Min(count, _length - _position);
        Array.Clear(buffer, offset, numToClear);
        _position += numToClear;

        return numToClear;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == _length)
        {
            if (_atEof)
            {
                throw new IOException("Attempt to read beyond end of stream");
            }

            _atEof = true;
            return Task.FromResult(0);
        }

        var numToClear = (int)Math.Min(count, _length - _position);
        Array.Clear(buffer, offset, numToClear);
        _position += numToClear;

        return Task.FromResult(numToClear);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == _length)
        {
            if (_atEof)
            {
                throw new IOException("Attempt to read beyond end of stream");
            }

            _atEof = true;
            return new(0);
        }

        var numToClear = (int)Math.Min(buffer.Length, _length - _position);
        buffer.Span.Slice(0, numToClear).Clear();
        _position += numToClear;

        return new(numToClear);
    }

    public override int Read(Span<byte> buffer)
    {
        if (_position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == _length)
        {
            if (_atEof)
            {
                throw new IOException("Attempt to read beyond end of stream");
            }

            _atEof = true;
            return 0;
        }

        var numToClear = (int)Math.Min(buffer.Length, _length - _position);
        buffer.Slice(0, numToClear).Clear();
        _position += numToClear;

        return numToClear;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var effectiveOffset = offset;
        if (origin == SeekOrigin.Current)
        {
            effectiveOffset += _position;
        }
        else if (origin == SeekOrigin.End)
        {
            effectiveOffset += _length;
        }

        _atEof = false;

        if (effectiveOffset < 0)
        {
            throw new IOException("Attempt to move before beginning of stream");
        }

        _position = effectiveOffset;
        return _position;
    }

    public sealed override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Attempt to write to read-only stream");
    public sealed override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Attempt to write to read-only stream");
    public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new InvalidOperationException("Attempt to write to read-only stream");
    public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Attempt to write to read-only stream");
    public sealed override void WriteByte(byte value) => throw new InvalidOperationException("Attempt to write to read-only stream");
    public override void SetLength(long value) => throw new InvalidOperationException("Attempt to change length of read-only stream");
}