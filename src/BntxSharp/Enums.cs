namespace BntxSharp;

public enum SurfaceFormat : uint
{
    Invalid = 0x0000,

    R4_G4_UNORM = 0x0101,
    R8_UNORM = 0x0201,
    R4_G4_B4_A4_UNORM = 0x0301,
    A4_B4_G4_R4_UNORM = 0x0401,
    R5_G5_B5_A1_UNORM = 0x0501,
    A1_B5_G5_R5_UNORM = 0x0601,
    R5_G6_B5_UNORM = 0x0701,
    B5_G6_R5_UNORM = 0x0801,
    R8_G8_UNORM = 0x0901,
    R8_G8_SNORM = 0x0902,
    R16_UNORM = 0x0A01,
    R16_UINT = 0x0A05,
    R8_G8_B8_A8_UNORM = 0x0B01,
    R8_G8_B8_A8_SNORM = 0x0B02,
    R8_G8_B8_A8_SRGB = 0x0B06,
    B8_G8_R8_A8_UNORM = 0x0C01,
    B8_G8_R8_A8_SRGB = 0x0C06,
    R9_G9_B9_E5_UNORM = 0x0D01,
    R10_G10_B10_A2_UNORM = 0x0E01,
    R11_G11_B10_UNORM = 0x0F01,
    R11_G11_B10_UINT = 0x0F05,
    B10_G11_R11_UNORM = 0x1001,
    R16_G16_UNORM = 0x1101,
    R24_G8_UNORM = 0x1201,
    R32_UNORM = 0x1301,
    R16_G16_B16_A16_UNORM = 0x1401,
    R32_G8_X24_UNORM = 0x1501,
    D32_FLOAT_S8X24_UINT = 0x1505,
    R32_G32_UNORM = 0x1601,
    R32_G32_B32_UNORM = 0x1701,
    R32_G32_B32_A32_UNORM = 0x1801,

    BC1_UNORM = 0x1A01,
    BC1_SRGB = 0x1A06,
    BC2_UNORM = 0x1B01,
    BC2_SRGB = 0x1B06,
    BC3_UNORM = 0x1C01,
    BC3_SRGB = 0x1C06,
    BC4_UNORM = 0x1D01,
    BC4_SNORM = 0x1D02,
    BC5_UNORM = 0x1E01,
    BC5_SNORM = 0x1E02,
    BC6_FLOAT = 0x1F05,
    BC6_UFLOAT = 0x1F0A,
    BC7_UNORM = 0x2001,
    BC7_SRGB = 0x2006,

    EAC_R11_UNORM = 0x2101,
    EAC_R11_G11_UNORM = 0x2201,
    ETC1_UNORM = 0x2301,
    ETC1_SRGB = 0x2306,
    ETC2_UNORM = 0x2401,
    ETC2_SRGB = 0x2406,
    ETC2_MASK_UNORM = 0x2501,
    ETC2_MASK_SRGB = 0x2506,
    ETC2_ALPHA_UNORM = 0x2601,
    ETC2_ALPHA_SRGB = 0x2606,

    PVRTC1_28PP_UNORM = 0x2701,
    PVRTC1_48PP_UNORM = 0x2801,
    PVRTC1_ALPHA_28PP_UNORM = 0x2901,
    PVRTC1_ALPHA_48PP_UNORM = 0x2A01,
    PVRTC2_ALPHA_28PP_UNORM = 0x2B01,
    PVRTC2_ALPHA_48PP_UNORM = 0x2C01,

    ASTC_4x4_UNORM = 0x2D01,
    ASTC_4x4_SRGB = 0x2D06,
    ASTC_5x4_UNORM = 0x2E01,
    ASTC_5x4_SRGB = 0x2E06,
    ASTC_5x5_UNORM = 0x2F01,
    ASTC_5x5_SRGB = 0x2F06,
    ASTC_6x5_UNORM = 0x3001,
    ASTC_6x5_SRGB = 0x3006,
    ASTC_6x6_UNORM = 0x3101,
    ASTC_6x6_SRGB = 0x3106,
    ASTC_8x5_UNORM = 0x3201,
    ASTC_8x5_SRGB = 0x3206,
    ASTC_8x6_UNORM = 0x3301,
    ASTC_8x6_SRGB = 0x3306,
    ASTC_8x8_UNORM = 0x3401,
    ASTC_8x8_SRGB = 0x3406,
    ASTC_10x5_UNORM = 0x3501,
    ASTC_10x5_SRGB = 0x3506,
    ASTC_10x6_UNORM = 0x3601,
    ASTC_10x6_SRGB = 0x3606,
    ASTC_10x8_UNORM = 0x3701,
    ASTC_10x8_SRGB = 0x3706,
    ASTC_10x10_UNORM = 0x3801,
    ASTC_10x10_SRGB = 0x3806,
    ASTC_12x10_UNORM = 0x3901,
    ASTC_12x10_SRGB = 0x3906,
    ASTC_12x12_UNORM = 0x3A01,
    ASTC_12x12_SRGB = 0x3A06,

    B5_G5_R5_A1_UNORM = 0x3B01,
}

public enum SurfaceFormatVariant : byte
{
    None = 0,
    UNorm = 1,
    SNorm = 2,
    UInt = 3,
    SInt = 4,
    Single = 5,
    Srgb = 6,
    UFloat = 0x0A,
}

public enum TileMode : ushort
{
    Default = 0,

    LinearAligned = 1,
}

public enum SurfaceDim : uint
{
    Dim1D = 0,
    Dim2D = 1,
    Dim3D = 2,
    DimCube = 3,
    Dim1DArray = 4,
    Dim2DArray = 5,
    Dim2DMsaa = 6,
    Dim2DMsaaArray = 7,
    DimCubeArray = 8,
}

public enum Dim : sbyte
{
    Undefined = 0,
    Dim1D = 1,
    Dim2D = 2,
    Dim3D = 3,
}

public enum ChannelType : byte
{
    Zero = 0,
    One = 1,
    Red = 2,
    Green = 3,
    Blue = 4,
    Alpha = 5,
}

[System.Flags]
public enum AccessFlags : uint
{
    None = 0,
    Texture = 0x20,
}
