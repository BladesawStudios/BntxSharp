namespace BntxSharp.Tegra;

/// <summary>How the 16-byte sectors of a GOB (64 bytes by 8 rows) are ordered in memory.</summary>
/// <remarks>
/// Both layouts place GOBs, and size block heights, the same way; only the order inside a GOB differs. A sector's
/// offset is built from its column (0-3) and row (0-7) bits. On the Tegra X1 those bits go to offsets
/// 16 (row bit 0), 32 (column bit 0), 64 (row bit 1), 128 (row bit 2) and 256 (column bit 1). On the Switch 2,
/// found from Hyrule Warriors: Age of Imprisonment's textures, column bit 0 and row bit 1 trade places: 32 is row
/// bit 1 and 64 is column bit 0.
/// </remarks>
public enum GobLayout
{
    TegraX1,
    Switch2,
}
