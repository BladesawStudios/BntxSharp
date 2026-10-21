# BntxSharp

A C# library for working with BNTX. Current confirmed game support is `Tears of the Kingdom`, `Breath of the Wild`, and `Super Mario Wonder`.
## Decoding and DDS

With [TexSharp](../TexSharp), a texture can be decoded or handed to an image editor:

```csharp
BntxTexture tex = BntxFile.LoadFile("texture.bntx").Textures[0];

byte[] rgba = tex.ToRgba8();                         // width * height * 4, mip 0, channels as stored
byte[] seen = tex.Render();                          // with the texture's channel swizzle applied
File.WriteAllBytes("texture.png", tex.ToPng());
File.WriteAllBytes("texture.dds", tex.ToDds().ToBytes());

tex.ReplaceFromDds(DdsImage.Parse(File.ReadAllBytes("texture.dds")));
```

`ReplaceFromDds` needs the same format as the texture; the new image's size and mip count can differ, and
the texture is resized to match. `ToDds(editable: true)` expands the small formats (R8, RG8 and the 16-bit packed
ones such as R5G6B5, RGBA4 and R5G5B5A1) to RGBA8 for image editors, and `ReplaceFromDds` collapses them again.
Single 2D textures only.

Every surface format decodes except ETC, EAC and PVRTC: BC1 to BC7, ASTC, the 8 and 16-bit packed formats, R16 (UNORM, SNORM,
UINT, SINT and FLOAT), the 16 and 32-bit two and four-channel formats, the packed floats and depth. A low byte
of 5 is nn::gfx's FLOAT, so `R16_UINT` (`0x0A05`) and `R11_G11_B10_UINT` (`0x0F05`) are
read as floats. The `SurfaceFormat` layouts (and the sizes used for swizzling) follow nn::gfx's ChannelFormat order:
TotK's vertex animation textures are `0x1505`, four half floats a texel, which is `R16_G16_B16_A16_FLOAT`.

## Licence

AGPL-3.0-or-later. See [license.md](license.md). Releases up to 1.0.0 were MIT; see [NOTICE.md](NOTICE.md).
