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

using System.IO;
using DiscUtils;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Partitions;

public class BiosPartitionedDiskBuilderTest
{
    [Fact]
    public void Basic()
    {
        long capacity = 10 * 1024 * 1024;
        var geometry = Geometry.FromCapacity(capacity);

        var builder = new BiosPartitionedDiskBuilder(capacity, geometry);
        builder.PartitionTable.Create(WellKnownPartitionType.WindowsNtfs, true);
        var partitionContent = SparseStream.FromStream(new MemoryStream((int)(builder.PartitionTable[0].SectorCount * 512)), Ownership.Dispose);
        partitionContent.Position = 4053;
        partitionContent.WriteByte(0xAf);
        builder.SetPartitionContent(0, partitionContent);

        var constructedStream = builder.Build() as SparseStream;

        var bpt = new BiosPartitionTable(constructedStream, geometry);
        Assert.Equal(1, bpt.Count);

        using var builtPartitionStream = bpt.Partitions[0].Open();
        builtPartitionStream.Position = 4053;
        Assert.Equal(0xAf, builtPartitionStream.ReadByte());

    }
}
