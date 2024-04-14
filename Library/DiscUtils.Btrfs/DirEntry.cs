//
// Copyright (c) 2017, Bianco Veigel
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
using System.IO;
using DiscUtils.Btrfs.Base;
using DiscUtils.Btrfs.Base.Items;
using DiscUtils.Internal;
using DiscUtils.Vfs;

namespace DiscUtils.Btrfs;

internal class DirEntry : VfsDirEntry
{
    private readonly InodeItem _inode;
    private readonly DirIndex _item;
    private readonly ulong _treeId;

    public DirEntry(ulong treeId, ulong objectId)
    {
        _treeId = treeId;
        ObjectId = objectId;
    }

    public DirEntry(ulong treeId, DirIndex item, InodeItem inode)
        :this(treeId, item.ChildLocation.ObjectId)
    {
        _inode = inode;
        _item = item;
    }

    public override DateTime CreationTimeUtc => _inode.CTime.DateTime.DateTime;

    public override DateTime LastAccessTimeUtc => _inode.ATime.DateTime.DateTime;

    public override DateTime LastWriteTimeUtc => _inode.MTime.DateTime.DateTime;

    public override bool HasVfsTimeInfo => true;

    public override FileAttributes FileAttributes
    {
        get
        {
            var unixFileType = _item.ChildType switch
            {
                DirItemChildType.Unknown => UnixFileType.None,
                DirItemChildType.RegularFile => UnixFileType.Regular,
                DirItemChildType.Directory => UnixFileType.Directory,
                DirItemChildType.CharDevice => UnixFileType.Character,
                DirItemChildType.BlockDevice => UnixFileType.Block,
                DirItemChildType.Fifo => UnixFileType.Fifo,
                DirItemChildType.Socket => UnixFileType.Socket,
                DirItemChildType.Symlink => UnixFileType.Link,
                DirItemChildType.ExtendedAttribute => UnixFileType.None,
                _ => throw new ArgumentOutOfRangeException(),
            };
            var result = Utilities.FileAttributesFromUnixFileType(unixFileType);

            if (_inode != null && (_inode.Flags & InodeFlag.Readonly) == InodeFlag.Readonly)
            {
                result |= FileAttributes.ReadOnly;
            }

            return result;
        }
    }

    public override bool HasVfsFileAttributes => _item != null;

    public override string FileName => _item.Name;

    public override bool IsDirectory => _item.ChildType == DirItemChildType.Directory;

    public override bool IsSymlink => _item.ChildType == DirItemChildType.Symlink;

    public override long UniqueCacheId
    {
        get
        {
            unchecked
            {
                var result = _inode == null?0:(long)_inode.TransId;
                result = (result * 397) ^ (long)_item.TransId;
                result = (result * 397) ^ (long)_item.ChildLocation.ObjectId;
                return result;
            }
        }
    }

    internal Directory CachedDirectory { get; set; }

    internal DirItemChildType Type => _item.ChildType;

    internal ulong ObjectId { get; private set; }

    internal ulong TreeId => _treeId;

    internal ulong FileSize => _inode.FileSize;

    internal bool IsSubtree => _item != null && _item.ChildLocation.ItemType == ItemType.RootItem;
}
