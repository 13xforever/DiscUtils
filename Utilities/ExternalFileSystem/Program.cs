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
using System.Linq;
using System.Text;
using DiscUtils;
using DiscUtils.Internal;
using DiscUtils.Setup;
using DiscUtils.Streams;
using DiscUtils.Vfs;
using LTRData.Extensions.Buffers;

namespace ExternalFileSystem;

class Program
{
    static void Main(string[] args)
    {
        SetupHelper.RegisterAssembly(typeof(Program).Assembly);

        var dummyFileSystemData = new MemoryStream(Encoding.ASCII.GetBytes("MYFS"));
        
        VirtualDisk dummyDisk = new DiscUtils.Raw.Disk(dummyFileSystemData, Ownership.None);
        var volMgr = new VolumeManager(dummyDisk);

        VolumeInfo volInfo = volMgr.GetLogicalVolumes()[0];
        var fsInfo = FileSystemManager.DetectFileSystems(volInfo)[0];

        var fs = fsInfo.Open(volInfo);
        ShowDir(fs.Root, 4);
    }

    private static void ShowDir(DiscDirectoryInfo dirInfo, int indent)
    {
        var indentStr = new string(' ', indent);
        Console.WriteLine($"{indentStr}{dirInfo.FullName,-50} [{dirInfo.CreationTimeUtc}]");
        foreach (var subDir in dirInfo.GetDirectories())
        {
            ShowDir(subDir, indent + 0);
        }

        foreach (var file in dirInfo.GetFiles())
        {
            Console.WriteLine($"{indentStr}{file.FullName,-50} [{file.CreationTimeUtc}]");
        }
    }
}

class MyDirEntry : VfsDirEntry
{
    private static long _nextId;

    private bool _isDir;
    private string _name;
    private long _id;

    public MyDirEntry(string name, bool isDir)
    {
        _name = name;
        _isDir = isDir;
        _id = _nextId++;
    }

    public override bool IsDirectory => _isDir;

    public override bool IsSymlink => false;

    public override string FileName => _name;

    public override bool HasVfsTimeInfo => true;

    public override DateTime LastAccessTimeUtc => new(1980, 10, 21, 11, 04, 22);

    public override DateTime LastWriteTimeUtc => new(1980, 10, 21, 11, 04, 22);

    public override DateTime CreationTimeUtc => new(1980, 10, 21, 11, 04, 22);

    public override bool HasVfsFileAttributes => true;

    public override FileAttributes FileAttributes => IsDirectory ? FileAttributes.Directory : FileAttributes.Normal;

    public override long UniqueCacheId => _id;
}

class MyFile : IVfsFile
{
    private MyDirEntry _dirEntry;

    public MyFile(MyDirEntry dirEntry)
    {
        _dirEntry = dirEntry;
    }

    public DateTime LastAccessTimeUtc
    {
        get => _dirEntry.LastAccessTimeUtc;
        set => throw new NotSupportedException();
    }

    public DateTime LastWriteTimeUtc
    {
        get => _dirEntry.LastWriteTimeUtc;
        set => throw new NotImplementedException();
    }

    public DateTime CreationTimeUtc
    {
        get => _dirEntry.CreationTimeUtc;
        set => throw new NotImplementedException();
    }

    public FileAttributes FileAttributes
    {
        get => _dirEntry.FileAttributes;
        set => throw new NotImplementedException();
    }

    public long FileLength => 10;

    public IBuffer FileContent
    {
        get
        {
            var result = new SparseMemoryBuffer(10);
            result.Write(0, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9], 0, 10);
            return result;
        }
    }

    IEnumerable<StreamExtent> IVfsFile.EnumerateAllocationExtents() => throw new NotImplementedException();
}

class MyDirectory : MyFile, IVfsDirectory<MyDirEntry, MyFile>
{
    private readonly FastDictionary<MyDirEntry> _entries;

    public MyDirectory(MyDirEntry dirEntry, bool isRoot)
        : base(dirEntry)
    {
        _entries = new(StringComparer.OrdinalIgnoreCase, entry => entry.FileName);

        if (isRoot)
        {
            for (var i = 0; i < 4; ++i)
            {
                _entries.Add(new MyDirEntry($"DIR{i}", true));
            }
        }

        for (var i = 0; i < 6; ++i)
        {
            _entries.Add(new MyDirEntry($"FILE{i}", false));
        }
    }

    public IReadOnlyDictionary<string, MyDirEntry> AllEntries => _entries;

    public MyDirEntry Self => null;

    public MyDirEntry GetEntryByName(string name)
        => AllEntries.TryGetValue(name, out var entry) ? entry : null;

    public MyDirEntry CreateNewFile(string name)
    {
        throw new NotSupportedException();
    }
}

class MyContext : VfsContext
{
}

class MyFileSystem : VfsFileSystem<MyDirEntry, MyFile, MyDirectory, MyContext>
{
    public MyFileSystem()
        : base(new DiscFileSystemOptions())
    {
        Context = new MyContext();
        RootDirectory = new MyDirectory(new MyDirEntry("", true), true);
    }

    public override bool IsCaseSensitive => false;

    public override string VolumeLabel => "Volume Label";

    protected override MyFile ConvertDirEntryToFile(MyDirEntry dirEntry)
    {
        if (dirEntry.IsDirectory)
        {
            return new MyDirectory(dirEntry, false);
        }
        else
        {
            return new MyFile(dirEntry);
        }
    }

    public override string FriendlyName => "My File System";

    public override bool CanWrite => false;

    public override long Size => throw new NotImplementedException();

    public override long UsedSpace => throw new NotImplementedException();

    public override long AvailableSpace => throw new NotImplementedException();
}

[VfsFileSystemFactory]
class MyFileSystemFactory : VfsFileSystemFactory
{
    public override IEnumerable<DiscUtils.FileSystemInfo> Detect(Stream stream, VolumeInfo volumeInfo)
    {
        var header = stream.ReadExactly(4);

        if ("MYFS"u8.SequenceEqual(header))
        {
            return SingleValueEnumerable.Get(new VfsFileSystemInfo("MyFs", "My File System", Open));
        }

        return [];
    }

    private MyFileSystem Open(Stream stream, VolumeInfo volInfo, FileSystemParameters parameters)
    {
        return new MyFileSystem();
    }
}
