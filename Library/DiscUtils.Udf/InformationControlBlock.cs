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
using DiscUtils.Streams;

namespace DiscUtils.Udf;

internal class InformationControlBlock : IByteArraySerializable
{
    public AllocationType AllocationType;
    public FileType FileType;
    public InformationControlBlockFlags Flags;
    public ushort MaxEntries;
    public LogicalBlockAddress ParentICBLocation;
    public uint PriorDirectEntries;
    public ushort StrategyParameter;
    public ushort StrategyType;

    public int Size => 20;

    public int ReadFrom(ReadOnlySpan<byte> buffer)
    {
        PriorDirectEntries = EndianUtilities.ToUInt32LittleEndian(buffer);
        StrategyType = EndianUtilities.ToUInt16LittleEndian(buffer.Slice(4));
        StrategyParameter = EndianUtilities.ToUInt16LittleEndian(buffer.Slice(6));
        MaxEntries = EndianUtilities.ToUInt16LittleEndian(buffer.Slice(8));
        FileType = (FileType)buffer[11];
        ParentICBLocation = EndianUtilities.ToStruct<LogicalBlockAddress>(buffer.Slice(12));

        var flagsField = EndianUtilities.ToUInt16LittleEndian(buffer.Slice(18));
        AllocationType = (AllocationType)(flagsField & 0x3);
        Flags = (InformationControlBlockFlags)(flagsField & 0xFFFC);

        return 20;
    }

    void IByteArraySerializable.WriteTo(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }
}