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
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vdi;

[VirtualDiskFactory("VDI", ".vdi")]
internal sealed class DiskFactory : VirtualDiskFactory
{
    public override string[] Variants => ["fixed", "dynamic"];

    public override VirtualDiskTypeInfo GetDiskTypeInformation(string variant)
    {
        return MakeDiskTypeInfo(variant);
    }

    public override DiskImageBuilder GetImageBuilder(string variant)
    {
        throw new NotImplementedException();
    }

    public override VirtualDisk CreateDisk(FileLocator locator, string variant, string path,
                                           VirtualDiskParameters diskParameters)
    {
        return variant switch
        {
            "fixed" => Disk.InitializeFixed(locator.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None),
                                    Ownership.Dispose, diskParameters.Capacity),
            "dynamic" => Disk.InitializeDynamic(
                                    locator.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None), Ownership.Dispose,
                                    diskParameters.Capacity),
            _ => throw new ArgumentException(
                                $"Unknown VDI disk variant '{variant}'",
                                nameof(variant)),
        };
    }

    public override VirtualDisk OpenDisk(string path, FileAccess access, bool useAsync = false)
    {
        return new Disk(path, access, useAsync);
    }

    public override VirtualDisk OpenDisk(FileLocator locator, string path, FileAccess access)
    {
        var share = access == FileAccess.Read ? FileShare.Read : FileShare.None;
        return new Disk(locator.Open(path, FileMode.Open, access, share), Ownership.Dispose);
    }

    public override VirtualDiskLayer OpenDiskLayer(FileLocator locator, string path, FileAccess access)
    {
        var mode = access == FileAccess.Read ? FileMode.Open : FileMode.OpenOrCreate;
        var share = access == FileAccess.Read ? FileShare.Read : FileShare.None;
        return new DiskImageFile(locator.Open(path, mode, access, share), Ownership.Dispose);
    }

    internal static VirtualDiskTypeInfo MakeDiskTypeInfo(string variant)
    {
        return new VirtualDiskTypeInfo
        {
            Name = "VDI",
            Variant = variant,
            CanBeHardDisk = true,
            DeterministicGeometry = true,
            PreservesBiosGeometry = true,
            CalcGeometry = c => GeometryRecord.FromCapacity(c).ToGeometry(c)
        };
    }
}