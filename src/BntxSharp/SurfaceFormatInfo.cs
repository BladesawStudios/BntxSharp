using System;
using System.Collections.Generic;

namespace BntxSharp;

public readonly struct SurfaceFormatInfo
{
    private SurfaceFormatInfo(int blockWidth, int blockHeight, int bytesPerBlock)
    {
        BlockWidth = blockWidth;
        BlockHeight = blockHeight;
        BytesPerBlock = bytesPerBlock;
    }

    public int BlockWidth { get; }

    public int BlockHeight { get; }

    public int BytesPerBlock { get; }

    public bool IsCompressed => BlockWidth > 1 || BlockHeight > 1;

    public static uint BaseOf(SurfaceFormat format) => (uint)format >> 8;

    public static SurfaceFormatVariant VariantOf(SurfaceFormat format) => (SurfaceFormatVariant)((uint)format & 0xFF);

    public static bool IsSrgb(SurfaceFormat format) => VariantOf(format) == SurfaceFormatVariant.Srgb;

    public static SurfaceFormat WithVariant(SurfaceFormat format, SurfaceFormatVariant variant) => (SurfaceFormat)((BaseOf(format) << 8) | (uint)variant);
    public static SurfaceFormatInfo For(SurfaceFormat format)
    {
        if (TryGet(format, out SurfaceFormatInfo info))
            return info;

        throw new NotSupportedException(
            $"Unknown surface format 0x{(uint)format:X4} (base 0x{BaseOf(format):X2}).");
    }

    public static bool TryGet(SurfaceFormat format, out SurfaceFormatInfo info) => Table.TryGetValue(BaseOf(format), out info);

    private static readonly Dictionary<uint, SurfaceFormatInfo> Table = new()
    {
        [0x01] = new(1, 1, 1),    // R4_G4
        [0x02] = new(1, 1, 1),    // R8
        [0x03] = new(1, 1, 2),    // R4_G4_B4_A4
        [0x04] = new(1, 1, 2),    // A4_B4_G4_R4
        [0x05] = new(1, 1, 2),    // R5_G5_B5_A1
        [0x06] = new(1, 1, 2),    // A1_B5_G5_R5
        [0x07] = new(1, 1, 2),    // R5_G6_B5
        [0x08] = new(1, 1, 2),    // B5_G6_R5
        [0x09] = new(1, 1, 2),    // R8_G8
        [0x0A] = new(1, 1, 2),    // R16
        [0x0B] = new(1, 1, 4),    // R8_G8_B8_A8
        [0x0C] = new(1, 1, 4),    // B8_G8_R8_A8
        [0x0D] = new(1, 1, 4),    // R9_G9_B9_E5
        [0x0E] = new(1, 1, 4),    // R10_G10_B10_A2
        [0x0F] = new(1, 1, 4),    // R11_G11_B10
        [0x10] = new(1, 1, 4),    // B10_G11_R11
        [0x11] = new(1, 1, 4),    // R16_G16
        [0x12] = new(1, 1, 4),    // R24_G8
        [0x13] = new(1, 1, 4),    // R32
        [0x14] = new(1, 1, 8),    // R16_G16_B16_A16
        [0x15] = new(1, 1, 8),    // R32_G8_X24
        [0x16] = new(1, 1, 8),    // R32_G32
        [0x17] = new(1, 1, 12),   // R32_G32_B32
        [0x18] = new(1, 1, 16),   // R32_G32_B32_A32

        [0x1A] = new(4, 4, 8),    // BC1
        [0x1B] = new(4, 4, 16),   // BC2
        [0x1C] = new(4, 4, 16),   // BC3
        [0x1D] = new(4, 4, 8),    // BC4
        [0x1E] = new(4, 4, 16),   // BC5
        [0x1F] = new(4, 4, 16),   // BC6
        [0x20] = new(4, 4, 16),   // BC7

        [0x21] = new(4, 4, 8),    // EAC_R11
        [0x22] = new(4, 4, 16),   // EAC_R11_G11
        [0x23] = new(4, 4, 8),    // ETC1
        [0x24] = new(4, 4, 8),    // ETC2
        [0x25] = new(4, 4, 8),    // ETC2_MASK
        [0x26] = new(4, 4, 16),   // ETC2_ALPHA

        [0x27] = new(8, 4, 8),    // PVRTC1 2bpp
        [0x28] = new(4, 4, 8),    // PVRTC1 4bpp
        [0x29] = new(8, 4, 8),    // PVRTC1 alpha 2bpp
        [0x2A] = new(4, 4, 8),    // PVRTC1 alpha 4bpp
        [0x2B] = new(8, 4, 8),    // PVRTC2 alpha 2bpp
        [0x2C] = new(4, 4, 8),    // PVRTC2 alpha 4bpp

        [0x2D] = new(4, 4, 16),   // ASTC 4x4
        [0x2E] = new(5, 4, 16),   // ASTC 5x4
        [0x2F] = new(5, 5, 16),   // ASTC 5x5
        [0x30] = new(6, 5, 16),   // ASTC 6x5
        [0x31] = new(6, 6, 16),   // ASTC 6x6
        [0x32] = new(8, 5, 16),   // ASTC 8x5
        [0x33] = new(8, 6, 16),   // ASTC 8x6
        [0x34] = new(8, 8, 16),   // ASTC 8x8
        [0x35] = new(10, 5, 16),  // ASTC 10x5
        [0x36] = new(10, 6, 16),  // ASTC 10x6
        [0x37] = new(10, 8, 16),  // ASTC 10x8
        [0x38] = new(10, 10, 16), // ASTC 10x10
        [0x39] = new(12, 10, 16), // ASTC 12x10
        [0x3A] = new(12, 12, 16), // ASTC 12x12

        [0x3B] = new(1, 1, 2),    // B5_G5_R5_A1
    };
}
