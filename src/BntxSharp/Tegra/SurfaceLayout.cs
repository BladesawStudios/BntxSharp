using System;

namespace BntxSharp.Tegra;

public static class SurfaceLayout
{
    public const int GobWidth = 64;

    public const int GobHeight = 8;

    public const int GobSize = GobWidth * GobHeight;

    public const int MaxBlockHeight = 16;

    public static int AlignUp(int value, int alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    public static long AlignUp(long value, long alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    public static int DivideUp(int value, int divisor) => (value + divisor - 1) / divisor;

    public static int RoundUpPow2(int value)
    {
        if (value <= 1)
            return 1;

        int result = 1;
        while (result < value)
            result <<= 1;

        return result;
    }

    public static int MipSize(int baseSize, int level) => Math.Max(1, baseSize >> level);

    /// <summary>The block height, in GOBs, the Tegra X1 swizzle gives a surface of this height (1 to 16).</summary>
    public static int BlockHeightMip0(int heightInBlocks)
    {
        int height = Math.Max(1, heightInBlocks);
        int heightAndHalf = height + height / 2;
        return heightAndHalf >= 128 ? 16 : heightAndHalf >= 64 ? 8 : heightAndHalf >= 32 ? 4 : heightAndHalf >= 16 ? 2 : 1;
    }

    public static int MipBlockHeight(int baseBlockHeight, int mipHeightInBlocks)
    {
        int gobRows = DivideUp(mipHeightInBlocks, GobHeight);
        return Math.Min(baseBlockHeight, RoundUpPow2(gobRows));
    }

    public static long TiledSize(
        int widthInBlocks, int heightInBlocks, int depth, int bytesPerBlock, int blockHeight)
    {
        long widthInGobs = DivideUp(widthInBlocks * bytesPerBlock, GobWidth);
        long heightInGobs = AlignUp(DivideUp(heightInBlocks, GobHeight), blockHeight);
        return widthInGobs * heightInGobs * GobSize * Math.Max(1, depth);
    }

    public static int WidthInGobs(int widthInBlocks, int bytesPerBlock) =>
        DivideUp(widthInBlocks * bytesPerBlock, GobWidth);

    public static long LinearSize(int widthInBlocks, int heightInBlocks, int depth, int bytesPerBlock) =>
        (long)widthInBlocks * heightInBlocks * Math.Max(1, depth) * bytesPerBlock;
}
