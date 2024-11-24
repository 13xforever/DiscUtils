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

namespace DiscUtils.Iso9660;

internal class BootValidationEntry
{
    private readonly byte[] _data;
    public byte HeaderId;
    public string ManfId;
    public byte PlatformId;

    public BootValidationEntry()
    {
        HeaderId = 1;
        PlatformId = 0;
        ManfId = ".Net DiscUtils";
    }

    public BootValidationEntry(byte[] src, int offset)
    {
        _data = StreamUtilities.GetUninitializedArray<byte>(32);
        System.Buffer.BlockCopy(src, offset, _data, 0, 32);

        HeaderId = _data[0];
        PlatformId = _data[1];
        ManfId = EncodingUtilities
            .GetLatin1Encoding()
            .GetString(_data, 4, 24).AsSpan().TrimEnd('\0').TrimEnd(' ').ToString();
    }

    public bool ChecksumValid
    {
        get
        {
            ushort total = 0;
            for (var i = 0; i < 16; ++i)
            {
                total += EndianUtilities.ToUInt16LittleEndian(_data, i * 2);
            }

            return total == 0;
        }
    }

    internal void WriteTo(byte[] buffer, int offset)
    {
        Array.Clear(buffer, offset, 0x20);
        buffer[offset + 0x00] = HeaderId;
        buffer[offset + 0x01] = PlatformId;

        EncodingUtilities
            .GetLatin1Encoding()
            .GetBytes(ManfId, 0, ManfId.Length, buffer, offset + 4);

        buffer[offset + 0x1E] = 0x55;
        buffer[offset + 0x1F] = 0xAA;
        EndianUtilities.WriteBytesLittleEndian(CalcChecksum(buffer, offset), buffer, offset + 0x1C);
    }

    private static ushort CalcChecksum(byte[] buffer, int offset)
    {
        ushort total = 0;
        for (var i = 0; i < 16; ++i)
        {
            total += EndianUtilities.ToUInt16LittleEndian(buffer, offset + i * 2);
        }

        return (ushort)(0 - total);
    }
}