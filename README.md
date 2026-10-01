# BntxSharp

A C# library for working with BNTX. Current confirmed game support is `Tears of the Kingdom`, `Breath of the Wild`, and `Super Mario Wonder`.
## Decoding and DDS

With [TexSharp](../TexSharp), a texture can be decoded or handed to an image editor:

```csharp
BntxTexture tex = BntxFile.LoadFile("texture.bntx").Textures[0];

byte[] rgba = tex.ToRgba8();                         // width * height * 4, mip 0
File.WriteAllBytes("texture.dds", tex.ToDds().ToBytes());

tex.ReplaceFromDds(DdsImage.Parse(File.ReadAllBytes("texture.dds")));
```

`ReplaceFromDds` needs the same format and size as the texture. 2D textures and 2D arrays only.

## Licence

AGPL-3.0-or-later. See [license.md](license.md). Releases up to 1.0.0 were MIT; see [NOTICE.md](NOTICE.md).
