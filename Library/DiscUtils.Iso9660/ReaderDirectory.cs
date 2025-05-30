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

using DiscUtils.Internal;
using DiscUtils.Streams;
using DiscUtils.Vfs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DiscUtils.Iso9660;

internal class ReaderDirectory : File, IVfsDirectory<ReaderDirEntry, File>
{
    private readonly FastDictionary<ReaderDirEntry> _records;

    public ReaderDirectory(IsoContext context, ReaderDirEntry dirEntry)
        : base(context, dirEntry)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(IsoUtilities.SectorSize);
        try
        {
            Array.Clear(buffer, 0, buffer.Length);
            Stream extent = new ExtentStream(_context.DataStream, dirEntry.Record.LocationOfExtent, uint.MaxValue, 0, 0);

            _records = new(StringComparer.OrdinalIgnoreCase, entry => entry.FileName);

            var totalLength = dirEntry.Record.DataLength;
            uint totalRead = 0;
            while (totalRead < totalLength)
            {
                var bytesRead = (int)Math.Min(IsoUtilities.SectorSize, totalLength - totalRead);

                extent.ReadExactly(buffer, 0, bytesRead);
                totalRead += (uint)bytesRead;

                uint pos = 0;
                while (pos < bytesRead && buffer[pos] != 0)
                {
                    var length = (uint)DirectoryRecord.ReadFrom(buffer.AsSpan((int)pos), context.VolumeDescriptor.CharacterEncoding, out var dr);

                    if (!IsoUtilities.IsSpecialDirectory(dr))
                    {
                        var childDirEntry = new ReaderDirEntry(_context, dr);

                        if (context.SuspDetected && !string.IsNullOrEmpty(context.RockRidgeIdentifier))
                        {
                            if (childDirEntry.SuspRecords == null || !childDirEntry.SuspRecords.HasEntry(context.RockRidgeIdentifier, "RE"))
                            {
                                _records.Add(childDirEntry);
                            }
                        }
                        else
                        {
                            _records.Add(childDirEntry);
                        }
                    }
                    else if (dr.FileIdentifier == "\0")
                    {
                        Self = new ReaderDirEntry(_context, dr);
                    }

                    pos += length;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override byte[] SystemUseData => Self.Record.SystemUseData;

    public IReadOnlyDictionary<string, ReaderDirEntry> AllEntries => _records;

    public ReaderDirEntry Self { get; }

    public ReaderDirEntry GetEntryByName(string name)
    {
        var anyVerMatch = name.IndexOf(';') < 0;
        var normName = IsoUtilities.NormalizeFileName(name.AsSpan()).ToUpper(CultureInfo.InvariantCulture).AsSpan();
        if (anyVerMatch)
        {
            normName = normName.Slice(0, normName.LastIndexOf(';') + 1);
        }

        foreach (var r in _records.Values)
        {
            var toComp = IsoUtilities.NormalizeFileName(r.FileName.AsSpan()).ToUpper(CultureInfo.InvariantCulture);
            if (!anyVerMatch && toComp.AsSpan().Equals(normName, StringComparison.CurrentCultureIgnoreCase))
            {
                return r;
            }

            if (anyVerMatch && toComp.AsSpan().StartsWith(normName, StringComparison.CurrentCultureIgnoreCase))
            {
                return r;
            }
        }

        return null;
    }

    public ReaderDirEntry CreateNewFile(string name)
    {
        throw new NotSupportedException();
    }
}