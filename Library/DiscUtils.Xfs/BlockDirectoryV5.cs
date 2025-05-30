//
// Copyright (c) 2019, Bianco Veigel
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
using DiscUtils.Streams;

namespace DiscUtils.Xfs;
internal class BlockDirectoryV5 : BlockDirectory
{
    public const uint HeaderMagicV5 = 0x58444233;

    public uint Crc { get; private set; }

    public ulong BlockNumber { get; private set; }
    
    public ulong LogSequenceNumber { get; private set; }

    public Guid Uuid { get; private set; }

    public ulong Owner { get; private set; }

    protected override int HeaderPadding => 4;

    public override int Size => 0x30 + 3 * 32 + 4;

    public BlockDirectoryV5(Context context) : base(context) { }

    public override bool HasValidMagic => Magic == HeaderMagicV5;

    protected override int ReadHeader(ReadOnlySpan<byte> buffer)
    {
        Magic = EndianUtilities.ToUInt32BigEndian(buffer);
        Crc = EndianUtilities.ToUInt32BigEndian(buffer.Slice(0x04));
        BlockNumber = EndianUtilities.ToUInt64BigEndian(buffer.Slice(0x08));
        LogSequenceNumber = EndianUtilities.ToUInt64BigEndian(buffer.Slice(0x10));
        Uuid = EndianUtilities.ToGuidBigEndian(buffer.Slice(0x18));
        Owner = EndianUtilities.ToUInt64BigEndian(buffer.Slice(0x28));
        return 0x30;
    }
}
