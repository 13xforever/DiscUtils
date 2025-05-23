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

//
// Based on "libbzip2", Copyright (C) 1996-2007 Julian R Seward.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Internal;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;

namespace DiscUtils.Compression;

/// <summary>
/// Implementation of a BZip2 decoder.
/// </summary>
public sealed class BZip2DecoderStream : ReadOnlyCompatibilityStream
{
    private readonly BigEndianBitStream _bitstream;

    private readonly byte[] _blockBuffer;
    private uint _blockCrc;
    private readonly BZip2BlockDecoder _blockDecoder;
    private Crc32 _calcBlockCrc;
    private uint _calcCompoundCrc;
    private uint _compoundCrc;
    private Stream _compressedStream;

    private bool _eof;
    private readonly Ownership _ownsCompressed;
    private long _position;
    private BZip2RleStream _rleStream;

    /// <summary>
    /// Initializes a new instance of the BZip2DecoderStream class.
    /// </summary>
    /// <param name="stream">The compressed input stream.</param>
    /// <param name="ownsStream">Whether ownership of stream passes to the new instance.</param>
    public BZip2DecoderStream(Stream stream, Ownership ownsStream)
    {
        _compressedStream = stream;
        _ownsCompressed = ownsStream;

        _bitstream = new BigEndianBitStream(new BufferedStream(stream));

        // The Magic BZh
        Span<byte> magic = stackalloc byte[3];
        magic[0] = (byte)_bitstream.Read(8);
        magic[1] = (byte)_bitstream.Read(8);
        magic[2] = (byte)_bitstream.Read(8);
        if (magic[0] != 0x42 || magic[1] != 0x5A || magic[2] != 0x68)
        {
            throw new InvalidDataException("Bad magic at start of stream");
        }

        // The size of the decompression blocks in multiples of 100,000
        var blockSize = (int)_bitstream.Read(8) - 0x30;
        if (blockSize is < 1 or > 9)
        {
            throw new InvalidDataException($"Unexpected block size in header: {blockSize}");
        }

        blockSize *= 100000;

        _rleStream = new BZip2RleStream();
        _blockDecoder = new BZip2BlockDecoder(blockSize);
        _blockBuffer = StreamUtilities.GetUninitializedArray<byte>(blockSize);

        if (ReadBlock() == 0)
        {
            _eof = true;
        }
    }

    /// <summary>
    /// Gets an indication of whether read access is permitted.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets an indication of whether seeking is permitted.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Gets the length of the stream (the capacity of the underlying buffer).
    /// </summary>
    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// Gets and sets the current position within the stream.
    /// </summary>
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Reads a number of bytes from the stream.
    /// </summary>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The start offset within the destination buffer.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(buffer);
#else
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
#endif

        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (_eof || buffer.IsEmpty)
        {
            return 0;
        }

        var numRead = _rleStream.Read(buffer);
        if (numRead == 0)
        {
            // If there was an existing block, check it's crc.
            if (_calcBlockCrc != null)
            {
                if (_blockCrc != _calcBlockCrc.Value)
                {
                    throw new InvalidDataException("Decompression failed - block CRC mismatch");
                }

                _calcCompoundCrc = ((_calcCompoundCrc << 1) | (_calcCompoundCrc >> 31)) ^ _blockCrc;
            }

            // Read a new block (if any), if none - check the overall CRC before returning
            if (ReadBlock() == 0)
            {
                _eof = true;
                if (_calcCompoundCrc != _compoundCrc)
                {
                    throw new InvalidDataException("Decompression failed - compound CRC");
                }

                return 0;
            }

            numRead = _rleStream.Read(buffer);
        }

        _calcBlockCrc.Process(buffer.Slice(0, numRead));

        // Pre-read next block, so a client that knows the decompressed length will still
        // have the overall CRC calculated.
        if (_rleStream.AtEof)
        {
            // If there was an existing block, check it's crc.
            if (_calcBlockCrc != null)
            {
                if (_blockCrc != _calcBlockCrc.Value)
                {
                    throw new InvalidDataException("Decompression failed - block CRC mismatch");
                }
            }

            _calcCompoundCrc = ((_calcCompoundCrc << 1) | (_calcCompoundCrc >> 31)) ^ _blockCrc;
            if (ReadBlock() == 0)
            {
                _eof = true;
                if (_calcCompoundCrc != _compoundCrc)
                {
                    throw new InvalidDataException("Decompression failed - compound CRC mismatch");
                }

                return numRead;
            }
        }

        _position += numRead;
        return numRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_eof || buffer.IsEmpty)
        {
            return 0;
        }

        var numRead = await _rleStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (numRead == 0)
        {
            // If there was an existing block, check it's crc.
            if (_calcBlockCrc != null)
            {
                if (_blockCrc != _calcBlockCrc.Value)
                {
                    throw new InvalidDataException("Decompression failed - block CRC mismatch");
                }

                _calcCompoundCrc = ((_calcCompoundCrc << 1) | (_calcCompoundCrc >> 31)) ^ _blockCrc;
            }

            // Read a new block (if any), if none - check the overall CRC before returning
            if (ReadBlock() == 0)
            {
                _eof = true;
                if (_calcCompoundCrc != _compoundCrc)
                {
                    throw new InvalidDataException("Decompression failed - compound CRC");
                }

                return 0;
            }

            numRead = await _rleStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        _calcBlockCrc.Process(buffer.Span.Slice(0, numRead));

        // Pre-read next block, so a client that knows the decompressed length will still
        // have the overall CRC calculated.
        if (_rleStream.AtEof)
        {
            // If there was an existing block, check it's crc.
            if (_calcBlockCrc != null)
            {
                if (_blockCrc != _calcBlockCrc.Value)
                {
                    throw new InvalidDataException("Decompression failed - block CRC mismatch");
                }
            }

            _calcCompoundCrc = ((_calcCompoundCrc << 1) | (_calcCompoundCrc >> 31)) ^ _blockCrc;
            if (ReadBlock() == 0)
            {
                _eof = true;
                if (_calcCompoundCrc != _compoundCrc)
                {
                    throw new InvalidDataException("Decompression failed - compound CRC mismatch");
                }

                return numRead;
            }
        }

        _position += numRead;
        return numRead;
    }

    /// <summary>
    /// Changes the current stream position.
    /// </summary>
    /// <param name="offset">The origin-relative stream position.</param>
    /// <param name="origin">The origin for the stream position.</param>
    /// <returns>The new stream position.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Releases underlying resources.
    /// </summary>
    /// <param name="disposing">Whether this method is called from Dispose.</param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (_compressedStream != null && _ownsCompressed == Ownership.Dispose)
                {
                    _compressedStream.Dispose();
                }

                _compressedStream = null;

                if (_rleStream != null)
                {
                    _rleStream.Dispose();
                    _rleStream = null;
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private int ReadBlock()
    {
        var marker = ReadMarker();
        if (marker == 0x314159265359)
        {
            var blockSize = _blockDecoder.Process(_bitstream, _blockBuffer, 0);
            _rleStream.Reset(_blockBuffer, 0, blockSize);
            _blockCrc = _blockDecoder.Crc;
            _calcBlockCrc = new Crc32BigEndian(Crc32Algorithm.Common);
            return blockSize;
        }

        if (marker == 0x177245385090)
        {
            _compoundCrc = ReadUint();
            return 0;
        }

        throw new InvalidDataException("Found invalid marker in stream");
    }

    private uint ReadUint()
    {
        uint val = 0;

        for (var i = 0; i < 4; ++i)
        {
            val = (val << 8) | _bitstream.Read(8);
        }

        return val;
    }

    private ulong ReadMarker()
    {
        ulong marker = 0;

        for (var i = 0; i < 6; ++i)
        {
            marker = (marker << 8) | _bitstream.Read(8);
        }

        return marker;
    }
}