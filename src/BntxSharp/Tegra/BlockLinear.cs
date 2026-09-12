using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace BntxSharp.Tegra;

public static class BlockLinear
{
    private const int SectorSize = 16;

    private const int SectorsPerGobRow = SurfaceLayout.GobWidth / SectorSize;

    public static void Deswizzle(
        ReadOnlySpan<byte> tiled, Span<byte> linear,
        int widthInBlocks, int heightInBlocks, int depth, int bytesPerBlock, int blockHeight) =>
        Transfer(tiled, linear, tiled.Length, widthInBlocks, heightInBlocks, depth,
            bytesPerBlock, blockHeight, toLinear: true);

    public static void Swizzle(
        ReadOnlySpan<byte> linear, Span<byte> tiled,
        int widthInBlocks, int heightInBlocks, int depth, int bytesPerBlock, int blockHeight) =>
        Transfer(linear, tiled, tiled.Length, widthInBlocks, heightInBlocks, depth,
            bytesPerBlock, blockHeight, toLinear: false);

    private static void Transfer(
        ReadOnlySpan<byte> source, Span<byte> destination, int tiledLength,
        int widthInBlocks, int heightInBlocks, int depth, int bytesPerBlock, int blockHeight,
        bool toLinear)
    {
        if (widthInBlocks <= 0 || heightInBlocks <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthInBlocks), "Surface dimensions must be positive.");
        if (bytesPerBlock <= 0)
            throw new ArgumentOutOfRangeException(nameof(bytesPerBlock));
        if (blockHeight <= 0 || (blockHeight & (blockHeight - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(blockHeight), "Block height must be a power of two.");

        depth = Math.Max(1, depth);

        long linearSize = SurfaceLayout.LinearSize(widthInBlocks, heightInBlocks, depth, bytesPerBlock);
        long tiledSize = SurfaceLayout.TiledSize(widthInBlocks, heightInBlocks, depth, bytesPerBlock, blockHeight);
        long needed = toLinear ? linearSize : tiledSize;

        if (destination.Length < needed)
            throw new ArgumentException(
                $"Destination is {destination.Length} bytes, needs {needed}.", nameof(destination));

        int widthInGobs = SurfaceLayout.WidthInGobs(widthInBlocks, bytesPerBlock);
        int gobColumnBytes = SurfaceLayout.GobSize * blockHeight;
        long blockRowBytes = (long)gobColumnBytes * widthInGobs;
        int blockRows = SurfaceLayout.GobHeight * blockHeight;
        long sliceBytes = tiledSize / depth;
        int rowBytes = widthInBlocks * bytesPerBlock;

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);

        for (int z = 0; z < depth; z++)
        {
            long sliceBase = sliceBytes * z;

            for (int gobY = 0; gobY < heightInBlocks; gobY += SurfaceLayout.GobHeight)
            {
                long gobRowBase = sliceBase
                    + (long)(gobY / blockRows) * blockRowBytes
                    + (long)(gobY % blockRows / SurfaceLayout.GobHeight) * SurfaceLayout.GobSize;

                for (int gobX = 0; gobX < widthInGobs; gobX++)
                {
                    long gobBase = gobRowBase + (long)gobX * gobColumnBytes;
                    int gobXBytes = gobX * SurfaceLayout.GobWidth;

                    int rows = Math.Min(SurfaceLayout.GobHeight, heightInBlocks - gobY);
                    for (int y = 0; y < rows; y++)
                    {
                        long rowBase = gobBase + (y >> 1) * 64 + (y & 1) * 16;
                        long linearRow = ((long)z * heightInBlocks + gobY + y) * rowBytes;

                        for (int sector = 0; sector < SectorsPerGobRow; sector++)
                        {
                            int xBytes = gobXBytes + sector * SectorSize;
                            if (xBytes >= rowBytes)
                                break;

                            long tiledOffset = rowBase + (sector >> 1) * 256 + (sector & 1) * 32;
                            long linearOffset = linearRow + xBytes;
                            int length = Math.Min(SectorSize, rowBytes - xBytes);

                            if (length < SectorSize || tiledOffset + SectorSize > tiledLength)
                            {
                                Partial(source, destination, tiledLength, tiledOffset, linearOffset, length, toLinear);
                                continue;
                            }

                            long from = toLinear ? tiledOffset : linearOffset;
                            long to = toLinear ? linearOffset : tiledOffset;

                            Unsafe.WriteUnaligned(
                                ref Unsafe.Add(ref destinationBase, (nint)to),
                                Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sourceBase, (nint)from)));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Transfers fewer than <see cref="SectorSize"/> bytes: a row whose width is not a whole
    /// number of sectors, or a tiled buffer that stops inside one.
    /// </summary>
    private static void Partial(
        ReadOnlySpan<byte> source, Span<byte> destination, int tiledLength,
        long tiledOffset, long linearOffset, int length, bool toLinear)
    {
        if (tiledOffset >= tiledLength)
            return;

        length = (int)Math.Min(length, tiledLength - tiledOffset);

        long from = toLinear ? tiledOffset : linearOffset;
        long to = toLinear ? linearOffset : tiledOffset;

        source.Slice((int)from, length).CopyTo(destination.Slice((int)to, length));
    }
}
