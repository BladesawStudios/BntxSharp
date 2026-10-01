using System;
using System.Collections.Generic;
using TexSharp;

namespace BntxSharp;

/// <summary>Decoding and DDS export for a <see cref="BntxTexture"/>, through TexSharp.</summary>
public static class BntxTextureExtensions
{
    /// <summary>The TexSharp format for the texture, or false if TexSharp has no equivalent.</summary>
    public static bool TryGetTextureFormat(this BntxTexture texture, out TextureFormat format, out bool srgb, out bool snorm)
        => BntxFormats.TryFromBntx((uint)texture.Format, out format, out srgb, out snorm);

    /// <summary>One mip of one array slice as width * height * 4 bytes of RGBA8.</summary>
    public static byte[] ToRgba8(this BntxTexture texture, int level = 0, int arrayLevel = 0)
    {
        TextureFormat format = RequireFormat(texture, out _, out bool snorm);
        int width = Math.Max(1, texture.Width >> level);
        int height = Math.Max(1, texture.Height >> level);
        return TextureDecoder.ToRgba8(format, texture.GetDeswizzledData(level, arrayLevel), width, height, snorm);
    }

    /// <summary>All mips of one array slice as a DDS. The pixel data is passed through untouched.</summary>
    public static DdsImage ToDds(this BntxTexture texture, int arrayLevel = 0)
    {
        TextureFormat format = RequireFormat(texture, out bool srgb, out bool snorm);

        List<byte[]> mips = [];
        for (int level = 0; level < texture.MipCount; level++)
            mips.Add(texture.GetDeswizzledData(level, arrayLevel));

        return new DdsImage(texture.Width, texture.Height, format, mips, srgb, snorm);
    }

    /// <summary>
    /// Writes a DDS over one array slice. The DDS has to be the same format and size as the texture and carry
    /// every mip it has; changing either means building a new texture, which this library doesn't do.
    /// </summary>
    public static void ReplaceFromDds(this BntxTexture texture, DdsImage dds, int arrayLevel = 0)
    {
        ArgumentNullException.ThrowIfNull(dds);
        TextureFormat format = RequireFormat(texture, out _, out _);

        if (dds.Format != format)
            throw new ArgumentException($"Texture '{texture.Name}' is {format}, the DDS is {dds.Format}.", nameof(dds));
        if (dds.Width != texture.Width || dds.Height != texture.Height)
            throw new ArgumentException(
                $"Texture '{texture.Name}' is {texture.Width}x{texture.Height}, the DDS is {dds.Width}x{dds.Height}.", nameof(dds));
        if (dds.Mips.Count < texture.MipCount)
            throw new ArgumentException($"Texture '{texture.Name}' has {texture.MipCount} mips, the DDS has {dds.Mips.Count}.", nameof(dds));

        for (int level = 0; level < texture.MipCount; level++)
            texture.SetDeswizzledData(dds.Mips[level], level, arrayLevel);
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
