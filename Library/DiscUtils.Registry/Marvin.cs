﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DiscUtils.Registry;

internal static class Marvin
{
    /// <summary>
    /// Convenience method to compute a Marvin hash and collapse it into a 32-bit hash.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeHash32(ReadOnlySpan<byte> data, ulong seed)
    {
        var hash64 = ComputeHash(data, seed);
        return ((int)(hash64 >> 32)) ^ (int)hash64;
    }

    /// <summary>
    /// Computes a 64-hash using the Marvin algorithm.
    /// </summary>
    public static long ComputeHash(ReadOnlySpan<byte> data, ulong seed)
    {
        var p0 = (uint)seed;
        var p1 = (uint)(seed >> 32);

        if (data.Length >= sizeof(uint))
        {
            var uData = MemoryMarshal.Cast<byte, uint>(data);

            for (var i = 0; i < uData.Length; i++)
            {
                p0 += uData[i];
                Block(ref p0, ref p1);
            }

            // byteOffset = data.Length - data.Length % 4
            // is equivalent to clearing last 2 bits of length
            // Using it directly gives a perf hit for short strings making it at least 5% or more slower.
            var byteOffset = data.Length & (~3);
            data = data.Slice(byteOffset);
        }

        switch (data.Length)
        {
            case 0:
                p0 += 0x80u;
                break;

            case 1:
                p0 += 0x8000u | data[0];
                break;

            case 2:
                p0 += 0x800000u | MemoryMarshal.Cast<byte, ushort>(data)[0];
                break;

            case 3:
                p0 += 0x80000000u | (((uint)data[2]) << 16) | MemoryMarshal.Cast<byte, ushort>(data)[0];
                break;

            default:
                Debug.Fail("Should not get here.");
                break;
        }

        Block(ref p0, ref p1);
        Block(ref p0, ref p1);

        return (((long)p1) << 32) | p0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Block(ref uint rp0, ref uint rp1)
    {
        var p0 = rp0;
        var p1 = rp1;

        p1 ^= p0;
        p0 = _rotl(p0, 20);

        p0 += p1;
        p1 = _rotl(p1, 9);

        p1 ^= p0;
        p0 = _rotl(p0, 27);

        p0 += p1;
        p1 = _rotl(p1, 19);

        rp0 = p0;
        rp1 = p1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint _rotl(uint value, int shift)
    {
        // This is expected to be optimized into a single rol (or ror with negated shift value) instruction
        return (value << shift) | (value >> (32 - shift));
    }

    public static ulong DefaultSeed { get; } = GenerateSeed();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    private static ulong GenerateSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return MemoryMarshal.Read<ulong>(bytes);
    }
#else
    private static ulong GenerateSeed()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = ArrayPool<byte>.Shared.Rent(sizeof(ulong));
        try
        {
            rng.GetBytes(bytes, 0, sizeof(ulong));
            return BitConverter.ToUInt64(bytes, 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }
#endif
}
