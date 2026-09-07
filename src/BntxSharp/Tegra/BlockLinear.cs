using System;

namespace BntxSharp.Tegra;

public static class BlockLinear
{
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

        for (int z = 0; z < depth; z++)
        {
            long sliceBase = sliceBytes * z;

            for (int y = 0; y < heightInBlocks; y++)
            {
                long rowBase = sliceBase
                    + (long)(y / blockRows) * blockRowBytes
                    + (long)(y % blockRows / SurfaceLayout.GobHeight) * SurfaceLayout.GobSize
                    + (y % SurfaceLayout.GobHeight / 2) * 64
                    + (y % 2) * 16;

                long linearRow = ((long)z * heightInBlocks + y) * widthInBlocks * bytesPerBlock;

                for (int x = 0; x < widthInBlocks; x++)
                {
                    int xBytes = x * bytesPerBlock;

                    long tiledOffset = rowBase
                        + (long)(xBytes / SurfaceLayout.GobWidth) * gobColumnBytes
                        + (xBytes % 64 / 32) * 256
                        + (xBytes % 32 / 16) * 32
                        + xBytes % 16;

                    if (tiledOffset + bytesPerBlock > tiledLength)
                        continue;

                    long linearOffset = linearRow + xBytes;
                    long from = toLinear ? tiledOffset : linearOffset;
                    long to = toLinear ? linearOffset : tiledOffset;

                    source.Slice((int)from, bytesPerBlock).CopyTo(destination.Slice((int)to, bytesPerBlock));
                }
            }
        }
    }
}
