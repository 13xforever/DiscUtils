//
// Copyright (c) 2016, Bianco Veigel
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
using DiscUtils.Streams;

namespace DiscUtils.Xfs;
internal class BlockDirectory : IByteArraySerializable
{
    private readonly Context _context;
    public const uint HeaderMagic = 0x58443242;

    public uint Magic { get; protected set; }

    public uint LeafCount { get; private set; }

    public uint LeafStale { get; private set; }

    public BlockDirectoryDataFree[] BestFree { get; private set; }

    public List<BlockDirectoryData> Entries { get; private set; }

    public virtual int Size => 16 + 3 * 32;

    protected virtual int ReadHeader(ReadOnlySpan<byte> buffer)
    {
        Magic = EndianUtilities.ToUInt32BigEndian(buffer);
        return 0x4;
    }

    protected virtual int HeaderPadding => 0;

    public BlockDirectory(Context context)
    {
        _context = context;
    }

    public virtual bool HasValidMagic => Magic == HeaderMagic;

    public int ReadFrom(ReadOnlySpan<byte> buffer)
    {
        var offset = ReadHeader(buffer);
        BestFree = new BlockDirectoryDataFree[3];
        for (var i = 0; i < BestFree.Length; i++)
        {
            var free = new BlockDirectoryDataFree();
            offset += free.ReadFrom(buffer.Slice(offset));
            BestFree[i] = free;
        }

        offset += HeaderPadding;

        LeafStale = EndianUtilities.ToUInt32BigEndian(buffer.Slice(buffer.Length - 0x4));
        LeafCount = EndianUtilities.ToUInt32BigEndian(buffer.Slice(buffer.Length - 0x8));
        var entries = new List<BlockDirectoryData>();
        var eof = buffer.Length - 0x8 - LeafCount*0x8;
        while (offset < eof)
        {
            BlockDirectoryData entry;
            if (buffer[offset] == 0xff && buffer[offset + 0x1] == 0xff)
            {
                //unused
                entry = new BlockDirectoryDataUnused();
            }
            else
            {
                entry = new BlockDirectoryDataEntry(_context);
            }

            offset += entry.ReadFrom(buffer.Slice(offset));
            entries.Add(entry);
        }

        Entries = entries;
        return buffer.Length - offset;
    }

    void IByteArraySerializable.WriteTo(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }
}
