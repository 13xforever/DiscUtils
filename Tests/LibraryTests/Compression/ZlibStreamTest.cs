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

using System.IO;
using System.IO.Compression;
using System.Text;
using DiscUtils.Compression;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Compression;

public class ZlibStreamTest
{
    [Fact]
    public void TestRoundtrip()
    {
        var testData = Encoding.ASCII.GetBytes("This is a test string");

        var compressedStream = new MemoryStream();

        using (var zs = new ZlibStream(compressedStream, CompressionMode.Compress, true))
        {
            zs.Write(testData, 0, testData.Length);
        }

        compressedStream.Position = 0;
        using var uzs = new ZlibStream(compressedStream, CompressionMode.Decompress, true);
        var outData = new byte[testData.Length];
        uzs.ReadExactly(outData, 0, outData.Length);
        Assert.Equal(testData, outData);

        // Should be end of stream
        Assert.Equal(-1, uzs.ReadByte());
    }

    [Fact]
    public void TestInvalidChecksum()
    {
        var testData = Encoding.ASCII.GetBytes("This is a test string");

        var compressedStream = new MemoryStream();

        using (var zs = new ZlibStream(compressedStream, CompressionMode.Compress, true))
        {
            zs.Write(testData, 0, testData.Length);
        }

        compressedStream.Seek(-2, SeekOrigin.End);
        compressedStream.Write([0, 0], 0, 2);

        compressedStream.Position = 0;
        Assert.Throws<InvalidDataException>(() =>
        {
            using var uzs = new ZlibStream(compressedStream, CompressionMode.Decompress, true);
            var outData = new byte[testData.Length];
            uzs.ReadExactly(outData, 0, outData.Length);
            Assert.Equal(testData, outData);

            // Should be end of stream
            Assert.Equal(-1, uzs.ReadByte());
        });
    }
}
