﻿//
// Copyright (c) 2017, Quamotion
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

using DiscUtils.Nfs;
using System.IO;
using Xunit;

namespace LibraryTests.Nfs;

public class Nfs3ModifyResultTest
{
    [Fact]
    public void RoundTripTest()
    {
        var result = new Nfs3ModifyResult()
        {
            CacheConsistency = new Nfs3WeakCacheConsistency(),
            Status = Nfs3Status.Ok
        };

        Nfs3ModifyResult clone = null;

        using (var stream = new MemoryStream())
        {
            var writer = new XdrDataWriter(stream);
            result.Write(writer);

            stream.Position = 0;
            var reader = new XdrDataReader(stream);
            clone = new Nfs3ModifyResult(reader);
        }

        Assert.Equal(result, clone);
    }
}
