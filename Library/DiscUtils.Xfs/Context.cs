//
// Copyright (c) 2008-2011, Kenneth Bell
// Copyright (c) 2016, Bianco Veigel
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
using DiscUtils.Vfs;

namespace DiscUtils.Xfs;
internal class Context : VfsContext
{
    public Stream RawStream { get; set; }

    public SuperBlock SuperBlock { get; set; }

    public AllocationGroup[] AllocationGroups { get; set; }
    
    public XfsFileSystemOptions Options { get; set; }

    public Inode GetInode(ulong number)
    {
        var inode = new Inode(number, this);
        var group = AllocationGroups[inode.AllocationGroup];
        group.LoadInode(ref inode);
        if (inode.Magic != Inode.InodeMagic)
        {
            throw new IOException("invalid inode magic");
        }

        return inode;
    }
}
