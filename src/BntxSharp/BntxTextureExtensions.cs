using System;
using System.Collections.Generic;
using BntxSharp.Tegra;
using TexSharp;

namespace BntxSharp;

/// <summary>Decoding and DDS export and import for a <see cref="BntxTexture"/>, through TexSharp.</summary>
public static class BntxTextureExtensions
{
    /// <summary>The TexSharp format for the texture, or false if TexSharp has no equivalent.</summary>
    public static bool TryGetTextureFormat(this BntxTexture texture, out TextureFormat format, out bool srgb, out bool snorm)
        => BntxFormats.TryFromBntx((uint)texture.Format, out format, out srgb, out snorm);

    /// <summary>One mip of one array slice as width * height * 4 bytes of RGBA8.</summary>
    public static byte[] ToRgba8(this BntxTexture texture, int level = 0, int arrayLevel = 0)
    {
        TextureFormat format = RequireFormat(texture, out _, out bool snorm);
        int width = SurfaceLayout.MipSize(texture.Width, level);
        int height = SurfaceLayout.MipSize(texture.Height, level);
        return TextureDecoder.ToRgba8(format, texture.GetDeswizzledData(level, arrayLevel), width, height, snorm);
    }

    /// <summary>The channel swizzle stored with the texture, which says how its channels are meant to be read.</summary>
    public static ChannelMap GetChannelMap(this BntxTexture texture)
        => texture.ChannelTypes.Length >= 4
            ? ChannelMap.FromBntx((byte)texture.ChannelTypes[0], (byte)texture.ChannelTypes[1], (byte)texture.ChannelTypes[2], (byte)texture.ChannelTypes[3])
            : ChannelMap.Identity;

    /// <summary>
    /// <see cref="ToRgba8"/> as the texture is meant to be seen: its channel swizzle applied, and for a BC5 normal map
    /// the blue channel rebuilt.
    /// </summary>
    public static byte[] Render(this BntxTexture texture, int level = 0, int arrayLevel = 0)
    {
        TextureFormat format = RequireFormat(texture, out _, out bool snorm);
        int width = SurfaceLayout.MipSize(texture.Width, level);
        int height = SurfaceLayout.MipSize(texture.Height, level);
        return TextureDecoder.Render(format, texture.GetDeswizzledData(level, arrayLevel), width, height, texture.GetChannelMap(), snorm);
    }

    /// <summary>A PNG of <see cref="Render"/>.</summary>
    public static byte[] ToPng(this BntxTexture texture, int level = 0, int arrayLevel = 0)
        => PngWriter.Encode(texture.Render(level, arrayLevel), SurfaceLayout.MipSize(texture.Width, level), SurfaceLayout.MipSize(texture.Height, level));

    /// <summary>
    /// All mips of one array slice as a DDS. The pixel data is passed through untouched, except with
    /// <paramref name="editable"/>: R8, RG8, R5G6B5 and RGBA4 are then expanded to RGBA8, which image
    /// editors can open. <see cref="ReplaceFromDds"/> collapses them again.
    /// </summary>
    public static DdsImage ToDds(this BntxTexture texture, int arrayLevel = 0, bool editable = false)
    {
        TextureFormat format = RequireFormat(texture, out bool srgb, out bool snorm);
        bool expand = editable && PixelFormats.CanRoundTripThroughRgba8(format);

        List<byte[]> mips = [];
        for (int level = 0; level < texture.MipCount; level++)
        {
            byte[] mip = texture.GetDeswizzledData(level, arrayLevel);
            if (expand)
                mip = PixelFormats.ExpandToRgba8(format, mip, SurfaceLayout.MipSize(texture.Width, level) * SurfaceLayout.MipSize(texture.Height, level));
            mips.Add(mip);
        }

        return expand
            ? new DdsImage(texture.Width, texture.Height, TextureFormat.Rgba8, mips)
            : new DdsImage(texture.Width, texture.Height, format, mips, srgb, snorm);
    }

    /// <summary>
    /// Replaces the texture's image with a DDS. The DDS has to be the same format; its size and mip count may
    /// differ from the texture's, and the texture is resized to match. An RGBA8 DDS is accepted for the small
    /// formats <c>ToDds(editable: true)</c> expands. The pixels are not converted. The texture's sRGB variant
    /// follows the DDS when the DDS states a colour space (<see cref="DdsImage.ColorSpaceKnown"/>).
    /// </summary>
    public static void ReplaceFromDds(this BntxTexture texture, DdsImage dds)
    {
        ArgumentNullException.ThrowIfNull(dds);
        TextureFormat format = RequireFormat(texture, out _, out _);

        if (texture.ArrayLength != 1)
            throw new NotSupportedException($"Texture '{texture.Name}' is an array of {texture.ArrayLength}; only single textures can be replaced.");
        if (texture.TileMode != TileMode.Default)
            throw new NotSupportedException($"Texture '{texture.Name}' is linear (pitch) rather than block-linear, which can't be replaced yet.");

        if (dds.Format == TextureFormat.Rgba8 && format != TextureFormat.Rgba8 && PixelFormats.CanRoundTripThroughRgba8(format))
        {
            var collapsed = new List<byte[]>();
            for (int level = 0; level < dds.Mips.Count; level++)
                collapsed.Add(PixelFormats.CollapseRgba8(format, dds.Mips[level], SurfaceLayout.MipSize(dds.Width, level) * SurfaceLayout.MipSize(dds.Height, level)));
            dds = new DdsImage(dds.Width, dds.Height, format, collapsed);
        }

        if (dds.Format != format)
            throw new ArgumentException($"Format mismatch: texture '{texture.Name}' is {format} but the DDS is {dds.Format}. Re-export the DDS as {format}.", nameof(dds));

        SurfaceFormatInfo info = texture.FormatInfo;
        int alignment = (int)Math.Max(1u, texture.Alignment);
        int baseBlockHeight = SurfaceLayout.BlockHeightMip0(SurfaceLayout.DivideUp(dds.Height, info.BlockHeight));

        int mipCount = dds.Mips.Count;
        long[] offsets = new long[mipCount];
        long[] tiledSizes = new long[mipCount];
        int[] blockHeights = new int[mipCount];
        long cursor = 0;
        for (int level = 0; level < mipCount; level++)
        {
            int widthInBlocks = SurfaceLayout.DivideUp(SurfaceLayout.MipSize(dds.Width, level), info.BlockWidth);
            int heightInBlocks = SurfaceLayout.DivideUp(SurfaceLayout.MipSize(dds.Height, level), info.BlockHeight);
            blockHeights[level] = SurfaceLayout.MipBlockHeight(baseBlockHeight, heightInBlocks);
            tiledSizes[level] = SurfaceLayout.TiledSize(widthInBlocks, heightInBlocks, 1, info.BytesPerBlock, blockHeights[level]);

            cursor = SurfaceLayout.AlignUp(cursor, alignment);
            offsets[level] = cursor;
            cursor += tiledSizes[level];
        }

        byte[] data = new byte[cursor];
        for (int level = 0; level < mipCount; level++)
        {
            int widthInBlocks = SurfaceLayout.DivideUp(SurfaceLayout.MipSize(dds.Width, level), info.BlockWidth);
            int heightInBlocks = SurfaceLayout.DivideUp(SurfaceLayout.MipSize(dds.Height, level), info.BlockHeight);
            BlockLinear.Swizzle(
                dds.Mips[level], data.AsSpan((int)offsets[level], (int)tiledSizes[level]),
                widthInBlocks, heightInBlocks, 1, info.BytesPerBlock, blockHeights[level]);
        }

        texture.Width = dds.Width;
        texture.Height = dds.Height;
        texture.Data = data;
        texture.MipOffsets.Clear();
        texture.MipOffsets.AddRange(offsets);

        int log2 = 0;
        while (1 << log2 < baseBlockHeight) log2++;
        texture.TextureLayout = (texture.TextureLayout & ~7u) | (uint)log2;

        // A DDS that can't state a colour space (BC1 to BC5 files) leaves the texture's as it is.
        if (dds.ColorSpaceKnown)
        {
            bool currentSrgb = SurfaceFormatInfo.IsSrgb(texture.Format);
            if (dds.IsSrgb && !currentSrgb)
                texture.Format = SurfaceFormatInfo.WithVariant(texture.Format, SurfaceFormatVariant.Srgb);
            else if (!dds.IsSrgb && currentSrgb)
                texture.Format = SurfaceFormatInfo.WithVariant(texture.Format, SurfaceFormatVariant.UNorm);
        }
    }

    /// <summary>
    /// Replaces one layer of an array texture with a DDS, leaving the other layers as they are. Every layer shares the
    /// texture's size, format and mip count. A DDS that differs in any of them is converted to fit (resized, its mips
    /// rebuilt, re-encoded) unless <paramref name="convert"/> is false, in which case it is refused. An RGBA8 DDS is
    /// accepted for the small formats <c>ToDds(editable: true)</c> expands without counting as a conversion.
    /// Returns what was changed to make the DDS fit, in words; empty if it fitted as it was.
    /// </summary>
    public static IReadOnlyList<string> ReplaceLayerFromDds(this BntxTexture texture, DdsImage dds, int arrayLevel, bool convert = true)
    {
        ArgumentNullException.ThrowIfNull(dds);
        TextureFormat format = RequireFormat(texture, out bool srgb, out bool snorm);

        int layers = Math.Max(1, texture.ArrayLength);
        if (arrayLevel < 0 || arrayLevel >= layers)
            throw new ArgumentOutOfRangeException(nameof(arrayLevel), arrayLevel, $"Texture '{texture.Name}' has {layers} layer(s).");

        if (dds.Format == TextureFormat.Rgba8 && format != TextureFormat.Rgba8 && PixelFormats.CanRoundTripThroughRgba8(format)
            && dds.Width == texture.Width && dds.Height == texture.Height && dds.Mips.Count == texture.MipCount)
        {
            var collapsed = new List<byte[]>();
            for (int level = 0; level < dds.Mips.Count; level++)
                collapsed.Add(PixelFormats.CollapseRgba8(format, dds.Mips[level], SurfaceLayout.MipSize(dds.Width, level) * SurfaceLayout.MipSize(dds.Height, level)));
            dds = new DdsImage(dds.Width, dds.Height, format, collapsed);
        }

        List<string> changes = TextureConverter.Differences(dds, format, texture.Width, texture.Height, texture.MipCount);
        IReadOnlyList<byte[]> mips = dds.Mips;
        if (changes.Count > 0)
        {
            if (!convert)
                throw new ArgumentException(
                    $"A layer has to match the texture: '{texture.Name}' is {texture.Width}x{texture.Height} {format} with {texture.MipCount} mip(s), " +
                    $"but the DDS is {dds.Width}x{dds.Height} {dds.Format} with {dds.Mips.Count}.", nameof(dds));
            mips = TextureConverter.Convert(dds, format, texture.Width, texture.Height, texture.MipCount, srgb, snorm);
        }

        for (int level = 0; level < mips.Count; level++)
            texture.SetDeswizzledData(mips[level], level, arrayLevel);
        return changes;
    }

    private static TextureFormat RequireFormat(BntxTexture texture, out bool srgb, out bool snorm)
    {
        if (texture.Depth != 1 || texture.SurfaceDim is SurfaceDim.Dim3D or SurfaceDim.DimCube or SurfaceDim.DimCubeArray)
            throw new NotSupportedException($"Texture '{texture.Name}' is {texture.SurfaceDim}; only 2D textures and 2D arrays are supported.");

        if (!texture.TryGetTextureFormat(out TextureFormat format, out srgb, out snorm))
            throw new NotSupportedException($"Texture '{texture.Name}' has format 0x{(uint)texture.Format:X4}, which TexSharp doesn't handle.");

        return format;
    }
}
