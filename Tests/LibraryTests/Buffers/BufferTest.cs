//
// Copyright (c) 2017, Glen Parker
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
using System.Linq;
using Xunit;

namespace LibraryTests.Buffers;

public class BufferTest
{
    [Fact]
    public void SparseMemoryBufferClear()
    {
        var memoryBuffer = new SparseMemoryBuffer(10);
        var buffer = new byte[20];

        memoryBuffer.Write(0, buffer, 0, 20);
        Assert.Equal(2, memoryBuffer.AllocatedChunks.Count());
        memoryBuffer.Clear(0, 20);
        Assert.Empty(memoryBuffer.AllocatedChunks);

        memoryBuffer.Write(0, buffer, 0, 15);
        Assert.Equal(2, memoryBuffer.AllocatedChunks.Count());
        memoryBuffer.Clear(0, 15);
        Assert.Single(memoryBuffer.AllocatedChunks);
    }
}
