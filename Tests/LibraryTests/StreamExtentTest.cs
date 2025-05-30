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
using System.Collections.Generic;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests;

public class StreamExtentTest
{
    [Fact]
    public void TestIntersect1()
    {
        var s1 = new StreamExtent[] {
            new(0,4)};
        var s2 = new StreamExtent[] {
            new(4,8)};
        var r = Array.Empty<StreamExtent>();

        Compare(r, StreamExtent.Intersect(s1, s2));
    }

    [Fact]
    public void TestIntersect2()
    {
        var s1 = new StreamExtent[] {
            new(0,4)};
        var s2 = new StreamExtent[] {
            new(3,8)};
        var r = new StreamExtent[] {
            new(3,1)};

        Compare(r, StreamExtent.Intersect(s1, s2));
    }

    [Fact]
    public void TestIntersect3()
    {
        var s1 = new StreamExtent[] {
            new(0,4),
            new(10, 10)};
        var s2 = new StreamExtent[] {
            new(3,8)};
        var r = new StreamExtent[] {
            new(3,1),
            new(10,1)};

        Compare(r, StreamExtent.Intersect(s1, s2));
    }

    [Fact]
    public void TestIntersect4()
    {
        var s1 = new StreamExtent[] {
            new(0,4)};
        var s2 = new StreamExtent[] {
            new(3,8)};
        var s3 = new StreamExtent[] {
            new(10,10)};
        var r = Array.Empty<StreamExtent>();

        Compare(r, StreamExtent.Intersect(s1, s2, s3));
    }

    [Fact]
    public void TestIntersect5()
    {
        var s1 = new StreamExtent[] {
            new(0,10)};
        var s2 = new StreamExtent[] {
            new(3,5)};
        var r = new StreamExtent[] {
            new(3,5)};

        Compare(r, StreamExtent.Intersect(s1, s2));
    }

    [Fact]
    public void TestUnion1()
    {
        var s1 = new StreamExtent[] {
            new(0,4)};
        var s2 = new StreamExtent[] {
            new(4,8)};
        var r = new StreamExtent[] {
            new(0,12)};

        Compare(r, StreamExtent.Union(s1, s2));
    }

    [Fact]
    public void TestUnion2()
    {
        var s1 = new StreamExtent[] {
            new(0,4)};
        var s2 = new StreamExtent[] {
            new(5,8)};
        var r = new StreamExtent[] {
            new(0,4),
            new(5,8)};

        Compare(r, StreamExtent.Union(s1, s2));
    }

    [Fact]
    public void TestUnion3()
    {
        var s1 = new StreamExtent[] {
            new(0,4)};
        var s2 = new StreamExtent[] {
            new(2,8)};
        var r = new StreamExtent[] {
            new(0,10)};

        Compare(r, StreamExtent.Union(s1, s2));
    }

    [Fact]
    public void TestUnion4()
    {
        var s1 = new StreamExtent[] {
            new(0,4),
            new(4,4)};
        var r = new StreamExtent[] {
            new(0,8)};

        Compare(r, StreamExtent.Union(s1));
    }

    [Fact]
    public void TestUnion5()
    {
        var r = Array.Empty<StreamExtent>();

        Compare(r, StreamExtent.Union());
    }

    [Fact]
    public void TestBlockCount()
    {
        var s = new StreamExtent[] {
            new(0,8),
            new(11, 4)
        };

        Assert.Equal(2, StreamExtent.BlockCount(s, 10));

        s = [
            new(0,8),
            new(9, 8)
        ];

        Assert.Equal(2, StreamExtent.BlockCount(s, 10));

        s = [
            new(3, 4),
            new(19, 4),
            new(44, 4)
        ];

        Assert.Equal(4, StreamExtent.BlockCount(s, 10));
    }

    [Fact]
    public void TestBlocks()
    {
        var s = new StreamExtent[] {
            new(0,8),
            new(11, 4)
        };

        var ranges = new List<Range<long,long>>(StreamExtent.Blocks(s, 10));

        Assert.Single(ranges);
        Assert.Equal(0, ranges[0].Offset);
        Assert.Equal(2, ranges[0].Count);

        s = [
            new(0,8),
            new(9, 8)
        ];

        ranges = new List<Range<long, long>>(StreamExtent.Blocks(s, 10));

        Assert.Single(ranges);
        Assert.Equal(0, ranges[0].Offset);
        Assert.Equal(2, ranges[0].Count);

        s = [
            new(3, 4),
            new(19, 4),
            new(44, 4)
        ];

        ranges = new List<Range<long, long>>(StreamExtent.Blocks(s, 10));

        Assert.Equal(2, ranges.Count);
        Assert.Equal(0, ranges[0].Offset);
        Assert.Equal(3, ranges[0].Count);
        Assert.Equal(4, ranges[1].Offset);
        Assert.Equal(1, ranges[1].Count);
    }

    private static void Compare(IEnumerable<StreamExtent> expected, IEnumerable<StreamExtent> actual)
    {
        var eList = new List<StreamExtent>(expected);
        var aList = new List<StreamExtent>(actual);

        var failed = false;
        var failedIndex = -1;
        if (eList.Count == aList.Count)
        {
            for (var i = 0; i < eList.Count; ++i)
            {
                if (eList[i] != aList[i])
                {
                    failed = true;
                    failedIndex = i;
                    break;
                }
            }
        }
        else
        {
            failed = true;
        }

        if (failed)
        {
            var str = $"Expected {eList.Count}(<";
            for (var i = 0; i < Math.Min(4, eList.Count); ++i)
            {
                str += $"{eList[i]},";
            }

            if (eList.Count > 4)
            {
                str += "...";
            }

            str += ">)";

            str += $", actual {aList.Count}(<";
            for (var i = 0; i < Math.Min(4, aList.Count); ++i)
            {
                str += $"{aList[i]},";
            }

            if (aList.Count > 4)
            {
                str += "...";
            }

            str += ">)";

            if (failedIndex != -1)
            {
                str += $" - different at index {failedIndex}";
            }

            Assert.Fail(str);
        }
    }
}
