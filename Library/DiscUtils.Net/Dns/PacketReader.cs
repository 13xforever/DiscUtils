﻿//
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
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Net.Dns;

internal sealed class PacketReader
{
    private readonly byte[] _data;

    public PacketReader(byte[] data)
    {
        _data = data;
    }

    public int Position { get; set; }

    public string ReadName()
    {
        var sb = new StringBuilder();

        var hasIndirected = false;
        var readPos = Position;

        while (_data[readPos] != 0)
        {
            var len = _data[readPos];
            switch (len & 0xC0)
            {
                case 0x00:
                    sb.Append(Encoding.UTF8.GetString(_data, readPos + 1, len));
                    sb.Append('.');
                    readPos += 1 + len;
                    if (!hasIndirected)
                    {
                        Position = readPos;
                    }

                    break;

                case 0xC0:
                    if (!hasIndirected)
                    {
                        Position += 2;
                    }

                    hasIndirected = true;
                    readPos = EndianUtilities.ToUInt16BigEndian(_data, readPos) & 0x3FFF;
                    break;

                default:
                    throw new NotImplementedException("Unknown control flags reading label");
            }
        }

        if (!hasIndirected)
        {
            Position++;
        }

        return sb.ToString();
    }

    public ushort ReadUShort()
    {
        var result = EndianUtilities.ToUInt16BigEndian(_data, Position);
        Position += 2;
        return result;
    }

    public int ReadInt()
    {
        var result = EndianUtilities.ToInt32BigEndian(_data, Position);
        Position += 4;
        return result;
    }

    public byte ReadByte()
    {
        var result = _data[Position];
        Position++;
        return result;
    }

    public byte[] ReadBytes(int count)
    {
        var result = new byte[count];
        System.Buffer.BlockCopy(_data, Position, result, 0, count);
        Position += count;
        return result;
    }
}