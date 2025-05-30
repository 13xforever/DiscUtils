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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DiscUtils.Compression;
using DiscUtils.Internal;
using DiscUtils.Streams;
using DiscUtils.Vfs;

namespace DiscUtils.HfsPlus;

internal class File : IVfsFileWithStreams
{
    private const string CompressionAttributeName = "com.apple.decmpfs";
    private readonly CommonCatalogFileInfo _catalogInfo;
    private readonly bool _hasCompressionAttribute;

    public File(Context context, CatalogNodeId nodeId, CommonCatalogFileInfo catalogInfo)
    {
        Context = context;
        NodeId = nodeId;
        _catalogInfo = catalogInfo;
        _hasCompressionAttribute =
            Context.Attributes.Find(new AttributeKey(NodeId, CompressionAttributeName)) != null;
    }

    protected Context Context { get; }

    protected CatalogNodeId NodeId { get; }

    public DateTime LastAccessTimeUtc
    {
        get => _catalogInfo.AccessTime;

        set => throw new NotSupportedException();
    }

    public DateTime LastWriteTimeUtc
    {
        get => _catalogInfo.ContentModifyTime;

        set => throw new NotSupportedException();
    }

    public DateTime CreationTimeUtc
    {
        get => _catalogInfo.CreateTime;

        set => throw new NotSupportedException();
    }

    public FileAttributes FileAttributes
    {
        get => Utilities.FileAttributesFromUnixFileType(_catalogInfo.FileSystemInfo.FileType);

        set => throw new NotSupportedException();
    }

    public long FileLength
    {
        get
        {
            if (_catalogInfo is not CatalogFileInfo fileInfo)
            {
                throw new InvalidOperationException();
            }

            return (long)fileInfo.DataFork.LogicalSize;
        }
    }

    public IBuffer FileContent
    {
        get
        {
            if (_catalogInfo is not CatalogFileInfo fileInfo)
            {
                throw new InvalidOperationException();
            }

            if (_hasCompressionAttribute)
            {
                // Open the compression attribute
                var compressionAttributeData =
                    Context.Attributes.Find(new AttributeKey(_catalogInfo.FileId, "com.apple.decmpfs"));
                var compressionAttribute = new CompressionAttribute();
                compressionAttribute.ReadFrom(compressionAttributeData);

                // There are multiple possibilities, not all of which are supported by DiscUtils.HfsPlus.
                // See FileCompressionType for a full description of all possibilities.
                switch (compressionAttribute.CompressionType)
                {
                    case FileCompressionType.ZlibAttribute:
                        if (compressionAttribute.UncompressedSize == compressionAttribute.AttrSize - 0x11)
                        {
                            // Inline, no compression, very small file
                            var stream = new MemoryStream(
                                compressionAttributeData,
                                CompressionAttribute.Size + 1,
                                (int)compressionAttribute.UncompressedSize,
                                false);

                            return new StreamBuffer(stream, Ownership.Dispose);
                        }
                        else
                        {
                            // Inline, but we must decompress
                            var stream = new MemoryStream(
                                compressionAttributeData,
                                CompressionAttribute.Size,
                                compressionAttributeData.Length - CompressionAttribute.Size,
                                false);

                            // The usage upstream will want to seek or set the position, the ZlibBuffer
                            // wraps around a zlibstream and allows for this (in a limited fashion).
                            var compressedStream = new ZlibStream(stream, CompressionMode.Decompress, false);
                            return new ZlibBuffer(compressedStream, Ownership.Dispose);
                        }

                    case FileCompressionType.ZlibResource:
                        // The data is stored in the resource fork.
                        var buffer = new FileBuffer(Context, fileInfo.ResourceFork, fileInfo.FileId);
                        var compressionFork = new CompressionResourceHeader();
                        Span<byte> compressionForkData = stackalloc byte[CompressionResourceHeader.Size];
                        buffer.Read(0, compressionForkData);
                        compressionFork.ReadFrom(compressionForkData);

                        // The data is compressed in a number of blocks. Each block originally accounted for
                        // 0x10000 bytes (that's 64 KB) of data. The compressed size may vary.
                        // The data in each block can be read using a SparseStream. The first block contains
                        // the zlib header but the others don't, so we read them directly as deflate streams.
                        // For each block, we create a separate stream which we later aggregate.
                        var blockHeader = new CompressionResourceBlockHead();
                        Span<byte> blockHeaderData = stackalloc byte[CompressionResourceBlockHead.Size];
                        buffer.Read(compressionFork.HeaderSize, blockHeaderData);
                        blockHeader.ReadFrom(blockHeaderData);

                        var blockCount = blockHeader.NumBlocks;
                        var blocks = new CompressionResourceBlock[blockCount];
                        var streams = new SparseStream[blockCount];

                        Span<byte> blockData = stackalloc byte[CompressionResourceBlock.Size];

                        for (var i = 0; i < blockCount; i++)
                        {
                            // Read the block data, first into a buffer and the into the class.
                            blocks[i] = new CompressionResourceBlock();

                            buffer.Read(
                                compressionFork.HeaderSize + CompressionResourceBlockHead.Size +
                                i * CompressionResourceBlock.Size,
                                blockData);
                            blocks[i].ReadFrom(blockData);

                            // Create a SubBuffer which points to the data window that corresponds to the block.
                            var subBuffer = new SubBuffer(
                                buffer,
                                compressionFork.HeaderSize + blocks[i].Offset + 6, blocks[i].DataSize);

                            // ... convert it to a stream
                            var stream = new BufferStream(subBuffer, FileAccess.Read);

                            // ... and create a deflate stream. Because we will concatenate the streams, the streams
                            // must report on their size. We know the size (0x10000) so we pass it as a parameter.
                            DeflateStream s = new SizedDeflateStream(stream, CompressionMode.Decompress, false, 0x10000);
                            streams[i] = SparseStream.FromStream(s, Ownership.Dispose);
                        }

                        // Finally, concatenate the streams together and that's about it.
                        var concatStream = new ConcatStream(Ownership.Dispose, streams);
                        return new ZlibBuffer(concatStream, Ownership.Dispose);

                    case FileCompressionType.RawAttribute:
                        // Inline, no compression, very small file
                        return new StreamBuffer(
                            new MemoryStream(
                                compressionAttributeData,
                                CompressionAttribute.Size + 1,
                                (int)compressionAttribute.UncompressedSize,
                                false),
                            Ownership.Dispose);

                    default:
                        throw new NotSupportedException($"The HfsPlus compression type {compressionAttribute.CompressionType} is not supported by DiscUtils.HfsPlus");
                }
            }

            return new FileBuffer(Context, fileInfo.DataFork, fileInfo.FileId);
        }
    }

    public IEnumerable<StreamExtent> EnumerateAllocationExtents()
    {
        if (_catalogInfo is not CatalogFileInfo fileInfo)
        {
            throw new InvalidOperationException();
        }

        if (_hasCompressionAttribute)
        {
            // Open the compression attribute
            var compressionAttributeData =
                Context.Attributes.Find(new AttributeKey(_catalogInfo.FileId, "com.apple.decmpfs"));
            var compressionAttribute = new CompressionAttribute();
            compressionAttribute.ReadFrom(compressionAttributeData);

            // There are multiple possibilities, not all of which are supported by DiscUtils.HfsPlus.
            // See FileCompressionType for a full description of all possibilities.
            switch (compressionAttribute.CompressionType)
            {
                case FileCompressionType.ZlibAttribute:
                case FileCompressionType.RawAttribute:
                    // Inline
                    return [];

                case FileCompressionType.ZlibResource:
                    // The data is stored in the resource fork.
                    return new FileBuffer(Context, fileInfo.ResourceFork, fileInfo.FileId).EnumerateAllocationExtents();

                default:
                    throw new NotSupportedException($"The HfsPlus compression type {compressionAttribute.CompressionType} is not supported by DiscUtils.HfsPlus");
            }
        }

        return new FileBuffer(Context, fileInfo.DataFork, fileInfo.FileId).EnumerateAllocationExtents();
    }

    public SparseStream CreateStream(string name)
    {
        throw new NotSupportedException();
    }

    public SparseStream OpenExistingStream(string name)
    {
        throw new NotImplementedException();
    }
}