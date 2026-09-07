using System;
using System.Collections.Generic;
using BntxSharp.Tegra;

namespace BntxSharp;

public sealed class BntxTexture
{
    public string Name { get; set; } = string.Empty;
    public byte Flags { get; set; }
    public Dim Dim { get; set; } = Dim.Dim2D;
    public TileMode TileMode { get; set; } = TileMode.Default;
    public ushort Swizzle { get; set; }
    public uint SampleCount { get; set; } = 1;
    public SurfaceFormat Format { get; set; }
    public AccessFlags AccessFlags { get; set; } = AccessFlags.Texture;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Depth { get; set; } = 1;
    public int ArrayLength { get; set; } = 1;
    public uint TextureLayout { get; set; }
    public uint TextureLayout2 { get; set; }
    public uint Alignment { get; set; } = 0x200;
    public ChannelType[] ChannelTypes { get; set; } = [ChannelType.Red, ChannelType.Green, ChannelType.Blue, ChannelType.Alpha];
    public SurfaceDim SurfaceDim { get; set; } = SurfaceDim.Dim2D;
    public byte[] Data { get; set; } = [];
    public List<long> MipOffsets { get; } = [];
    public int MipCount => MipOffsets.Count;
    public List<BntxUserData> UserData { get; } = [];
    public int BlockHeightLog2 => (int)(TextureLayout & 7);
    public SurfaceFormatInfo FormatInfo => SurfaceFormatInfo.For(Format);

    public int MipWidthInBlocks(int level) => SurfaceLayout.DivideUp(SurfaceLayout.MipSize(Width, level), FormatInfo.BlockWidth);

    public int MipHeightInBlocks(int level) => SurfaceLayout.DivideUp(SurfaceLayout.MipSize(Height, level), FormatInfo.BlockHeight);

    public int MipBlockHeight(int level) => SurfaceLayout.MipBlockHeight(1 << BlockHeightLog2, MipHeightInBlocks(level));

    public int MipDepth(int level) => SurfaceLayout.MipSize(Depth, level);

    public long MipTiledSize(int level) => SurfaceLayout.TiledSize(MipWidthInBlocks(level), MipHeightInBlocks(level), MipDepth(level), FormatInfo.BytesPerBlock, MipBlockHeight(level));

    public long ArraySliceStride
    {
        get
        {
            long total = 0;
            for (int level = 0; level < MipCount; level++)
                total += MipTiledSize(level);

            return SurfaceLayout.AlignUp(total, (long)SurfaceLayout.GobSize * (1 << BlockHeightLog2));
        }
    }

    public byte[] GetDeswizzledData(int level = 0, int arrayLevel = 0)
    {
        ValidateLevel(level, arrayLevel);

        int widthInBlocks = MipWidthInBlocks(level);
        int heightInBlocks = MipHeightInBlocks(level);
        int depth = MipDepth(level);
        SurfaceFormatInfo info = FormatInfo;

        ReadOnlySpan<byte> tiled = SliceOf(level, arrayLevel);
        byte[] linear = new byte[SurfaceLayout.LinearSize(widthInBlocks, heightInBlocks, depth, info.BytesPerBlock)];

        if (TileMode == TileMode.LinearAligned)
        {
            tiled.Slice(0, Math.Min(tiled.Length, linear.Length)).CopyTo(linear);
            return linear;
        }

        BlockLinear.Deswizzle(
            tiled, linear, widthInBlocks, heightInBlocks, depth,
            info.BytesPerBlock, MipBlockHeight(level));

        return linear;
    }

    public void SetDeswizzledData(ReadOnlySpan<byte> linear, int level = 0, int arrayLevel = 0)
    {
        ValidateLevel(level, arrayLevel);

        int widthInBlocks = MipWidthInBlocks(level);
        int heightInBlocks = MipHeightInBlocks(level);
        int depth = MipDepth(level);
        SurfaceFormatInfo info = FormatInfo;

        long expected = SurfaceLayout.LinearSize(widthInBlocks, heightInBlocks, depth, info.BytesPerBlock);
        if (linear.Length < expected)
            throw new ArgumentException(
                $"Mip {level} needs {expected} linear bytes, got {linear.Length}.", nameof(linear));

        Span<byte> tiled = SliceOf(level, arrayLevel);

        if (TileMode == TileMode.LinearAligned)
        {
            linear.Slice(0, Math.Min(tiled.Length, (int)expected)).CopyTo(tiled);
            return;
        }

        BlockLinear.Swizzle(
            linear, tiled, widthInBlocks, heightInBlocks, depth,
            info.BytesPerBlock, MipBlockHeight(level));
    }

    private Span<byte> SliceOf(int level, int arrayLevel)
    {
        long start = MipOffsets[level] + ArraySliceStride * arrayLevel;
        if (start < 0 || start > Data.Length)
            throw new InvalidOperationException($"Texture '{Name}' mip {level} slice {arrayLevel} starts at {start}, past its {Data.Length} bytes.");

        long end = level + 1 < MipCount
            ? MipOffsets[level + 1] + ArraySliceStride * arrayLevel
            : start + MipTiledSize(level);

        end = Math.Min(end, Data.Length);
        return Data.AsSpan((int)start, (int)Math.Max(0, end - start));
    }

    private void ValidateLevel(int level, int arrayLevel)
    {
        if (level < 0 || level >= MipCount)
            throw new ArgumentOutOfRangeException(nameof(level), $"Texture '{Name}' has {MipCount} mip levels.");
        if (arrayLevel < 0 || arrayLevel >= ArrayLength)
            throw new ArgumentOutOfRangeException(nameof(arrayLevel), $"Texture '{Name}' has {ArrayLength} array slices.");
    }
}
