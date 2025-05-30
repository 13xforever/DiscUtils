//
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


using System;
using System.IO;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils.Lvm;
internal class PhysicalVolume
{
    public const ushort SECTOR_SIZE = 512;
    private const uint INITIAL_CRC = 0xf597a6cf;

    public readonly PhysicalVolumeLabel PhysicalVolumeLabel;
    public readonly PvHeader PvHeader;
    public readonly VolumeGroupMetadata VgMetadata;
    public Stream Content { get; private set; }

    public PhysicalVolume(PhysicalVolumeLabel physicalVolumeLabel, Stream content)
    {
        PhysicalVolumeLabel = physicalVolumeLabel;
        content.Position = (long)(physicalVolumeLabel.Sector * SECTOR_SIZE);
        Span<byte> buffer = stackalloc byte[SECTOR_SIZE];
        content.ReadExactly(buffer);
        PvHeader = new PvHeader();
        PvHeader.ReadFrom(buffer.Slice((int)physicalVolumeLabel.Offset));
        if (PvHeader.MetadataDiskAreas.Count > 0)
        {
            var area = PvHeader.MetadataDiskAreas[0];

            content.Position = (long)area.Offset;
            VgMetadata = content.ReadStruct<VolumeGroupMetadata>((int)area.Length);
        }

        Content = content;
    }

    public static bool TryOpen(PartitionInfo volumeInfo, out PhysicalVolume pv)
    {
        var content = volumeInfo.Open();
        return TryOpen(content, out pv);
    }

    public static bool TryOpen(Stream content, out PhysicalVolume pv)
    {
        pv = null;
        if (!SearchLabel(content, out var label))
        {
            return false;
        }

        pv = new PhysicalVolume(label, content);

        return true;
    }

    public static bool CanOpen(PartitionInfo volumeInfo)
    {
        using var content = volumeInfo.Open();
        return SearchLabel(content, out _);
    }

    private static bool SearchLabel(Stream content, out PhysicalVolumeLabel pvLabel)
    {
        pvLabel = null;
        content.Position = 0;
        Span<byte> buffer = stackalloc byte[SECTOR_SIZE];
        for (uint i = 0; i < 4; i++)
        {
            if (content.ReadMaximum(buffer) != SECTOR_SIZE)
            {
                return false;
            }

            var label = EncodingUtilities
                .GetLatin1Encoding()
                .GetString(buffer.Slice(0x0, 0x8));

            if (label == PhysicalVolumeLabel.LABEL_ID)
            {
                pvLabel = new PhysicalVolumeLabel();
                pvLabel.ReadFrom(buffer);
                if (pvLabel.Sector != i)
                {
                    //Invalid PV Sector;
                    return false;
                }

                if (pvLabel.Crc != pvLabel.CalculatedCrc)
                {
                    //Invalid PV CRC
                    return false;
                }

                if (pvLabel.Label2 != PhysicalVolumeLabel.LVM2_LABEL)
                {
                    //Invalid LVM2 Label
                    return false;
                }

                return true;
            }
        }

        return false;
    }

    private static readonly uint[] crctab = [
        0x00000000, 0x1db71064, 0x3b6e20c8, 0x26d930ac,
        0x76dc4190, 0x6b6b51f4, 0x4db26158, 0x5005713c,
        0xedb88320, 0xf00f9344, 0xd6d6a3e8, 0xcb61b38c,
        0x9b64c2b0, 0x86d3d2d4, 0xa00ae278, 0xbdbdf21c
    ];

    /// <summary>
    /// LVM2.2.02.79:lib/misc/crc.c:_calc_crc_old()
    /// </summary>
    internal static uint CalcCrc(ReadOnlySpan<byte> buffer)
    {
        var crc = INITIAL_CRC;
        var i = 0;
        while (i < buffer.Length)
        {
            crc ^= buffer[i];
            crc = (crc >> 4) ^ crctab[crc & 0xf];
            crc = (crc >> 4) ^ crctab[crc & 0xf];
            i++;
        }

        return crc;

    }
}
