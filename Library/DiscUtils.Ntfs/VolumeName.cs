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
using System.IO;
using System.Text;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;

namespace DiscUtils.Ntfs;

internal sealed class VolumeName : IByteArraySerializable, IDiagnosticTraceable
{
    public VolumeName() {}

    public VolumeName(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public int Size => Encoding.Unicode.GetByteCount(Name);

    public int ReadFrom(ReadOnlySpan<byte> buffer)
    {
        Name = EndianUtilities.LittleEndianUnicodeBytesToString(buffer);
        return buffer.Length;
    }

    public void WriteTo(Span<byte> buffer)
    {
        Encoding.Unicode.GetBytes(Name.AsSpan(), buffer);
    }

    public void Dump(TextWriter writer, string indent)
    {
        writer.WriteLine($"{indent}  Volume Name: {Name}");
    }
}