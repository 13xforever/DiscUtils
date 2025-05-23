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


using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscUtils.Xfs;
internal class BTreeExtentRoot : IByteArraySerializable
{
    public ushort Level { get; protected set; }

    public ushort NumberOfRecords { get; protected set; }

    public ulong[] Keys { get; private set; }

    public ulong[] Pointer { get; private set; }

    public Dictionary<ulong, BTreeExtentHeader> Children { get; private set; }

    public int Size => 4 + (0x9 * 0x16);

    public int ReadFrom(ReadOnlySpan<byte> buffer)
    {
        Level = EndianUtilities.ToUInt16BigEndian(buffer);
        NumberOfRecords = EndianUtilities.ToUInt16BigEndian(buffer.Slice(2));
        var offset = 0x4;
        Keys = new ulong[NumberOfRecords];
        Pointer = new ulong[NumberOfRecords];
        for (var i = 0; i < NumberOfRecords; i++)
        {
            Keys[i] = EndianUtilities.ToUInt64BigEndian(buffer.Slice(offset + i * 0x8));
        }

        offset += ((buffer.Length - offset)/16)*8;
        for (var i = 0; i < NumberOfRecords; i++)
        {
            Pointer[i] = EndianUtilities.ToUInt64BigEndian(buffer.Slice(offset + i * 0x8));
        }

        return Size;
    }

    /// <inheritdoc />
    void IByteArraySerializable.WriteTo(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public void LoadBtree(Context context)
    {
        Children = new Dictionary<ulong, BTreeExtentHeader>(NumberOfRecords);
        for (var i = 0; i < NumberOfRecords; i++)
        {
            BTreeExtentHeader child;
            if (Level == 1)
            {
                if (context.SuperBlock.SbVersion == 5)
                {
                    child = new BTreeExtentLeafV5();
                }
                else
                {
                    child = new BTreeExtentLeaf();
                }
            }
            else
            {
                if (context.SuperBlock.SbVersion == 5)
                {
                    child = new BTreeExtentNodeV5();
                }
                else
                {
                    child = new BTreeExtentNode();
                }
            }

            var data = context.RawStream;
            data.Position = Extent.GetOffset(context, Pointer[i]);
            child.ReadFrom(data, (int)context.SuperBlock.Blocksize);
            if (context.SuperBlock.SbVersion < 5 && child.Magic != BTreeExtentHeader.BtreeMagic ||
                context.SuperBlock.SbVersion == 5 && child.Magic != BTreeExtentHeaderV5.BtreeMagicV5)
            {
                throw new IOException("invalid btree directory magic");
            }

            child.LoadBtree(context);
            Children.Add(Keys[i], child);
        }
    }

    public IEnumerable<Extent> GetExtents()
    {
        return Children.SelectMany(child => child.Value.GetExtents());
    }
}
