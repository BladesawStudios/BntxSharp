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
the texture is resized to match. `ToDds(editable: true)` expands the small formats (R8, RG8, R5G6B5, RGBA4)
to RGBA8 for image editors, and `ReplaceFromDds` collapses them again. Single 2D textures only.

## Licence

AGPL-3.0-or-later. See [license.md](license.md). Releases up to 1.0.0 were MIT; see [NOTICE.md](NOTICE.md).
