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
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using DiscUtils.Internal;
using DiscUtils.Streams;
using DiscUtils.Vfs;
using LTRData.Extensions.Buffers;

namespace DiscUtils.Iso9660;

internal sealed class ReaderDirEntry : VfsDirEntry
{
    private readonly IsoContext _context;
    private readonly string _fileName;
    internal readonly List<DirectoryRecord> _records = [];

    public ReaderDirEntry(IsoContext context, DirectoryRecord dirRecord)
    {
        _context = context;
        _fileName = dirRecord.FileIdentifier;

        var rockRidge = !string.IsNullOrEmpty(_context.RockRidgeIdentifier);

        if (context.SuspDetected && dirRecord.SystemUseData != null)
        {
            SuspRecords = new SuspRecords(_context, dirRecord.SystemUseData);
        }

        if (rockRidge && SuspRecords != null)
        {
            // The full name is taken from this record, even if it's a child-link record
            var nameEntries = SuspRecords.GetEntries(_context.RockRidgeIdentifier, "NM");
            var rrName = new StringBuilder();
            if (nameEntries?.Count > 0)
            {
                foreach (PosixNameSystemUseEntry nameEntry in nameEntries)
                {
                    rrName.Append(nameEntry.NameData);
                }

                _fileName = rrName.ToString();
            }

            // If this is a Rock Ridge child link, replace the dir record with that from the 'self' record
            // in the child directory.
            var clEntry = SuspRecords.GetEntry<ChildLinkSystemUseEntry>(_context.RockRidgeIdentifier, "CL");
            if (clEntry != null)
            {
                _context.DataStream.Position = clEntry.ChildDirLocation * _context.VolumeDescriptor.LogicalBlockSize;

                var firstSector = ArrayPool<byte>.Shared.Rent(_context.VolumeDescriptor.LogicalBlockSize);
                try
                {
                    _context.DataStream.ReadExactly(firstSector, 0, _context.VolumeDescriptor.LogicalBlockSize);

                    DirectoryRecord.ReadFrom(firstSector, _context.VolumeDescriptor.CharacterEncoding, out dirRecord);
                    if (dirRecord.SystemUseData != null)
                    {
                        SuspRecords = new SuspRecords(_context, dirRecord.SystemUseData);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(firstSector);
                }
            }
        }

        LastAccessTimeUtc =dirRecord.RecordingDateAndTime;
        LastWriteTimeUtc = dirRecord.RecordingDateAndTime;
        CreationTimeUtc = dirRecord.RecordingDateAndTime;

        if (rockRidge && SuspRecords != null)
        {
            var tfEntry =
                SuspRecords.GetEntry<FileTimeSystemUseEntry>(_context.RockRidgeIdentifier, "TF");

            if (tfEntry != null)
            {
                if ((tfEntry.TimestampsPresent & FileTimeSystemUseEntry.Timestamps.Access) != 0)
                {
                    LastAccessTimeUtc = tfEntry.AccessTime;
                }

                if ((tfEntry.TimestampsPresent & FileTimeSystemUseEntry.Timestamps.Modify) != 0)
                {
                    LastWriteTimeUtc = tfEntry.ModifyTime;
                }

                if ((tfEntry.TimestampsPresent & FileTimeSystemUseEntry.Timestamps.Creation) != 0)
                {
                    CreationTimeUtc = tfEntry.CreationTime;
                }
            }
        }
        
        _records.Add(dirRecord);
    }

    public override DateTime CreationTimeUtc { get; }

    public override FileAttributes FileAttributes
    {
        get
        {
            FileAttributes attrs = 0;

            if (!string.IsNullOrEmpty(_context.RockRidgeIdentifier))
            {
                // If Rock Ridge PX info is present, derive the attributes from the RR info.
                var pfi =
                    SuspRecords.GetEntry<PosixFileInfoSystemUseEntry>(_context.RockRidgeIdentifier, "PX");
                if (pfi != null)
                {
                    attrs = Utilities.FileAttributesFromUnixFileType((UnixFileType)((pfi.FileMode >> 12) & 0xF));
                }

                if (_fileName.StartsWith('.'))
                {
                    attrs |= FileAttributes.Hidden;
                }
            }

            attrs |= FileAttributes.ReadOnly;

            if ((_records[0].Flags & FileFlags.Directory) != 0)
            {
                attrs |= FileAttributes.Directory;
            }

            if ((_records[0].Flags & FileFlags.Hidden) != 0)
            {
                attrs |= FileAttributes.Hidden;
            }

            return attrs;
        }
    }

    public override string FileName => _fileName;

    public override bool HasVfsFileAttributes => true;

    public override bool HasVfsTimeInfo => true;

    public override bool IsDirectory => (_records[0].Flags & FileFlags.Directory) != 0;

    public override bool IsSymlink => false;

    public override DateTime LastAccessTimeUtc { get; }

    public override DateTime LastWriteTimeUtc { get; }

    [Obsolete("Please use RecordExtents property instead.")]
    public DirectoryRecord Record => _records[0];
    public ReadOnlyCollection<DirectoryRecord> RecordExtents => _records.AsReadOnly();
    public long RecordExtentsDataLength => _records.Sum(r => r.DataLength);

    public SuspRecords SuspRecords { get; }

    public override long UniqueCacheId => ((long)_records[0].LocationOfExtent << 32) | _records[0].DataLength;
}