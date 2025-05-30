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
using DiscUtils.Streams.Compatibility;

namespace DiscUtils.Iscsi;

internal class CommandRequest
{
    private readonly Connection _connection;

    private readonly ulong _lun;

    public CommandRequest(Connection connection, ulong lun)
    {
        _connection = connection;
        _lun = lun;
    }

    public byte[] GetBytes(ScsiCommand cmd, ReadOnlySpan<byte> immediateData, bool isFinalData,
                           bool willRead, bool willWrite, uint expected)
    {
        var _basicHeader = new BasicHeaderSegment
        {
            Immediate = cmd.ImmediateDelivery,
            OpCode = OpCode.ScsiCommand,
            FinalPdu = isFinalData,
            TotalAhsLength = 0,
            DataSegmentLength = immediateData.Length,
            InitiatorTaskTag = _connection.Session.CurrentTaskTag
        };

        var buffer = new byte[48 + MathUtilities.RoundUp(immediateData.Length, 4)];
        _basicHeader.WriteTo(buffer);
        buffer[1] = PackAttrByte(isFinalData, willRead, willWrite, cmd.TaskAttributes);
        EndianUtilities.WriteBytesBigEndian(_lun, buffer, 8);
        EndianUtilities.WriteBytesBigEndian(expected, buffer, 20);
        EndianUtilities.WriteBytesBigEndian(_connection.Session.CommandSequenceNumber, buffer, 24);
        EndianUtilities.WriteBytesBigEndian(_connection.ExpectedStatusSequenceNumber, buffer, 28);
        cmd.WriteTo(buffer, 32);

        if (!immediateData.IsEmpty)
        {
            immediateData.CopyTo(buffer.AsSpan(48));
        }

        return buffer;
    }

    private static byte PackAttrByte(bool isFinalData, bool expectReadFromTarget, bool expectWriteToTarget,
                                     TaskAttributes taskAttr)
    {
        byte value = 0;

        if (isFinalData)
        {
            value |= 0x80;
        }

        if (expectReadFromTarget)
        {
            value |= 0x40;
        }

        if (expectWriteToTarget)
        {
            value |= 0x20;
        }

        value |= (byte)((int)taskAttr & 0x3);

        return value;
    }
}