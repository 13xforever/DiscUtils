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
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Wim;

/// <summary>
/// Provides access to the contents of WIM (Windows Imaging) files.
/// </summary>
public class WimFile
{
    private readonly FileHeader _fileHeader;
    private readonly Stream _fileStream;
    private Dictionary<uint, List<ResourceInfo>> _resources;

    /// <summary>
    /// Initializes a new instance of the WimFile class.
    /// </summary>
    /// <param name="stream">A stream of the WIM file contents.</param>
    public WimFile(Stream stream)
    {
        _fileStream = stream;

        _fileHeader = stream.ReadStruct<FileHeader>(512);

        if (!_fileHeader.IsValid())
        {
            throw new IOException("Not a valid WIM file");
        }

        if (_fileHeader.TotalParts != 1)
        {
            throw new NotSupportedException("Multi-part WIM file");
        }

        ReadResourceTable();
    }

    /// <summary>
    /// Gets the (zero-based) index of the bootable image.
    /// </summary>
    public int BootImage => (int)_fileHeader.BootIndex;

    /// <summary>
    /// Gets the version of the file format.
    /// </summary>
    public int FileFormatVersion => (int)_fileHeader.Version;

    /// <summary>
    /// Gets the identifying GUID for this WIM file.
    /// </summary>
    public Guid Guid => _fileHeader.WimGuid;

    /// <summary>
    /// Gets the number of disk images within this file.
    /// </summary>
    public int ImageCount => (int)_fileHeader.ImageCount;

    /// <summary>
    /// Gets the embedded manifest describing the file and the contained images.
    /// </summary>
    public string Manifest
    {
        get
        {
            using var reader = new StreamReader(OpenResourceStream(_fileHeader.XmlDataHeader), true);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Gets a particular image within the file (zero-based index).
    /// </summary>
    /// <param name="index">The index of the image to retrieve.</param>
    /// <returns>The image as a file system.</returns>
    /// <remarks>The XML manifest file uses a one-based index, whereas this
    /// method is zero-based.</remarks>
    public WimFileSystem GetImage(int index)
    {
        return new WimFileSystem(this, index);
    }

    internal ShortResourceHeader LocateImage(int index)
    {
        var i = 0;

        using var s = OpenResourceStream(_fileHeader.OffsetTableHeader);
        long numRead = 0;
        Span<byte> resBuffer = stackalloc byte[ResourceInfo.Size];
        while (numRead < s.Length)
        {
            s.ReadExactly(resBuffer);
            numRead += ResourceInfo.Size;

            var info = new ResourceInfo();
            info.Read(resBuffer);

            if ((info.Header.Flags & ResourceFlags.MetaData) != 0)
            {
                if (i == index)
                {
                    return info.Header;
                }

                ++i;
            }
        }

        return null;
    }

    internal ShortResourceHeader LocateResource(byte[] hash)
    {
        var hashHash = EndianUtilities.ToUInt32LittleEndian(hash, 0);

        if (!_resources.TryGetValue(hashHash, out var headers))
        {
            return null;
        }

        foreach (var header in headers)
        {
            if (Utilities.AreEqual(header.Hash, hash))
            {
                return header.Header;
            }
        }

        return null;
    }

    internal SparseStream OpenResourceStream(ShortResourceHeader hdr)
    {
        SparseStream fileSectionStream = new SubStream(_fileStream, Ownership.None, hdr.FileOffset,
            hdr.CompressedSize);
        if ((hdr.Flags & ResourceFlags.Compressed) == 0)
        {
            return fileSectionStream;
        }

        return new FileResourceStream(fileSectionStream, hdr, (_fileHeader.Flags & FileFlags.LzxCompression) != 0,
            _fileHeader.CompressionSize);
    }

    private void ReadResourceTable()
    {
        _resources = [];
        using var s = OpenResourceStream(_fileHeader.OffsetTableHeader);
        long numRead = 0;
        Span<byte> resBuffer = stackalloc byte[ResourceInfo.Size];
        while (numRead < s.Length)
        {
            s.ReadExactly(resBuffer);
            numRead += ResourceInfo.Size;

            var info = new ResourceInfo();
            info.Read(resBuffer);

            var hashHash = EndianUtilities.ToUInt32LittleEndian(info.Hash, 0);

            if (!_resources.TryGetValue(hashHash, out var res))
            {
                res = new List<ResourceInfo>(1);

                _resources[hashHash] = res;
            }

            res.Add(info);
        }
    }
}