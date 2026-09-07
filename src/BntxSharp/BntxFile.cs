using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BntxSharp.Internal;
using BntxSharp.Tegra;

namespace BntxSharp;

public sealed class BntxFile
{
    private const int HeaderSize = 0x20;
    private const int ContainerOffset = 0x20;
    private const long MemoryPoolOffset = 0x58;
    private const long InfoPointerOffset = 0x198;
    private const int TextureHeaderSize = 0xA0;
    private const int TextureScratchSize = 0x100;
    private const int TextureMipOffsets = TextureHeaderSize + TextureScratchSize * 2;
    private const int BlockHeaderSize = 0x10;
    private const int UserDataEntrySize = 0x40;
    private const int DictionaryNodeSize = 0x10;
    public string Name { get; set; } = "textures";
    public uint Version { get; set; } = 0x0004_0000;
    public byte AlignmentShift { get; set; } = 12;
    public byte TargetAddressSize { get; set; } = 64;
    public ushort Flag { get; set; }
    public List<BntxTexture> Textures { get; } = [];

    public static BntxFile Load(ReadOnlySpan<byte> data)
    {
        BntxFile file = new();
        file.Read(data);
        return file;
    }

    public static BntxFile Load(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return Load(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    public static BntxFile LoadFile(string path) => Load(File.ReadAllBytes(path));

    public static bool IsBntx(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[0] == (byte)'B' && data[1] == (byte)'N' &&
        data[2] == (byte)'T' && data[3] == (byte)'X';

    public byte[] Save()
    {
        Layout layout = Plan();
        byte[] output = new byte[layout.FileSize];
        Write(output, layout);
        return output;
    }

    public void Save(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        byte[] data = Save();
        stream.Write(data, 0, data.Length);
    }

    public void SaveFile(string path) => File.WriteAllBytes(path, Save());

    public BntxTexture? Find(string name)
    {
        foreach (BntxTexture texture in Textures)
            if (string.Equals(texture.Name, name, StringComparison.Ordinal))
                return texture;

        return null;
    }


    private void Read(ReadOnlySpan<byte> data)
    {
        if (!IsBntx(data))
            throw new InvalidDataException("Not a BNTX file: missing the 'BNTX' signature.");
        if (data.Length < HeaderSize)
            throw new InvalidDataException($"Truncated BNTX: {data.Length} bytes.");

        ushort bom = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x0C));
        if (bom != 0xFEFF)
            throw new InvalidDataException(
                $"Unsupported byte order mark 0x{bom:X4}; only little-endian BNTX files are known.");

        Version = ReadU32(data, 0x08);
        AlignmentShift = data[0x0E];
        TargetAddressSize = data[0x0F];
        Flag = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x14));

        uint fileNameOffset = ReadU32(data, 0x10);
        uint fileSize = ReadU32(data, 0x1C);
        if (fileSize > int.MaxValue)
            throw new InvalidDataException(
                $"BNTX declares {fileSize} bytes; this reader addresses files up to {int.MaxValue}.");
        if (fileSize > data.Length)
            throw new InvalidDataException(
                $"BNTX declares {fileSize} bytes but only {data.Length} were supplied.");

        Name = ReadStringAt(data, (long)fileNameOffset - 2);

        if (!Matches(data, ContainerOffset, "NX  "))
            throw new InvalidDataException("Unsupported BNTX: expected an 'NX  ' texture container.");

        int count = (int)ReadU32(data, ContainerOffset + 0x04);
        long infoPointers = (long)ReadU64(data, ContainerOffset + 0x08);

        Textures.Clear();
        for (int i = 0; i < count; i++)
        {
            long position = (long)ReadU64(data, infoPointers + i * 8);
            Textures.Add(ReadTexture(data, position));
        }
    }

    private static BntxTexture ReadTexture(ReadOnlySpan<byte> data, long position)
    {
        if (!Matches(data, position, "BRTI"))
            throw new InvalidDataException($"Expected a 'BRTI' texture block at 0x{position:X}.");

        BntxTexture texture = new()
        {
            Flags = data[(int)position + 0x10],
            Dim = (Dim)(sbyte)data[(int)position + 0x11],
            TileMode = (TileMode)BinaryPrimitives.ReadUInt16LittleEndian(data.Slice((int)position + 0x12)),
            Swizzle = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice((int)position + 0x14)),
            SampleCount = ReadU32(data, position + 0x18),
            Format = (SurfaceFormat)ReadU32(data, position + 0x1C),
            AccessFlags = (AccessFlags)ReadU32(data, position + 0x20),
            Width = (int)ReadU32(data, position + 0x24),
            Height = (int)ReadU32(data, position + 0x28),
            Depth = (int)ReadU32(data, position + 0x2C),
            ArrayLength = (int)ReadU32(data, position + 0x30),
            TextureLayout = ReadU32(data, position + 0x34),
            TextureLayout2 = ReadU32(data, position + 0x38),
            Alignment = ReadU32(data, position + 0x54),
            SurfaceDim = (SurfaceDim)ReadU32(data, position + 0x5C),
        };

        int mipCount = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice((int)position + 0x16));
        uint imageSize = ReadU32(data, position + 0x50);

        texture.ChannelTypes =
        [
            (ChannelType)data[(int)position + 0x58],
            (ChannelType)data[(int)position + 0x59],
            (ChannelType)data[(int)position + 0x5A],
            (ChannelType)data[(int)position + 0x5B],
        ];

        texture.Name = ReadStringAt(data, (long)ReadU64(data, position + 0x60));

        long userData = (long)ReadU64(data, position + 0x78);
        long userDictionary = (long)ReadU64(data, position + 0x98);
        if (userData != 0 && userDictionary != 0)
        {
            int entries = (int)ReadU32(data, userDictionary + 4);
            for (int i = 0; i < entries; i++)
                texture.UserData.Add(ReadUserData(data, userData + i * UserDataEntrySize));
        }

        long mipPointers = (long)ReadU64(data, position + 0x70);
        if (mipCount <= 0)
            throw new InvalidDataException($"Texture '{texture.Name}' declares {mipCount} mip levels.");

        long baseOffset = (long)ReadU64(data, mipPointers);
        for (int level = 0; level < mipCount; level++)
            texture.MipOffsets.Add((long)ReadU64(data, mipPointers + level * 8) - baseOffset);

        if (baseOffset + imageSize > data.Length)
            throw new InvalidDataException(
                $"Texture '{texture.Name}' image data runs past the end of the file.");

        texture.Data = data.Slice((int)baseOffset, (int)imageSize).ToArray();
        return texture;
    }

    private static BntxUserData ReadUserData(ReadOnlySpan<byte> data, long position)
    {
        BntxUserData entry = new()
        {
            Name = ReadStringAt(data, (long)ReadU64(data, position)),
            Type = (UserDataType)ReadU32(data, position + 0x14),
        };

        long values = (long)ReadU64(data, position + 0x08);
        int count = (int)ReadU32(data, position + 0x10);

        switch (entry.Type)
        {
            case UserDataType.Int32:
                entry.Integers = new int[count];
                for (int i = 0; i < count; i++)
                    entry.Integers[i] = (int)ReadU32(data, values + i * 4);
                break;

            case UserDataType.Single:
                entry.Singles = new float[count];
                for (int i = 0; i < count; i++)
                    entry.Singles[i] = BitConverter.Int32BitsToSingle((int)ReadU32(data, values + i * 4));
                break;

            case UserDataType.Byte:
                entry.Bytes = data.Slice((int)values, count).ToArray();
                break;

            case UserDataType.String:
                entry.Strings = new string[count];
                for (int i = 0; i < count; i++)
                    entry.Strings[i] = ReadStringAt(data, (long)ReadU64(data, values + i * 8));
                break;

            default:
                throw new NotSupportedException(
                    $"User data entry '{entry.Name}' has type {entry.Type}, whose layout is not " +
                    "established - no Breath of the Wild file uses it. Please report the file.");
        }

        return entry;
    }


    private sealed class Layout
    {
        public long StringBlock;
        public long DictionaryOffset;
        public long FirstTexture;
        public long DataBlock;
        public long DataStart;
        public long RelocationOffset;
        public long FileSize;
        public long TextureContentEnd;
        public long[] TexturePositions = [];
        public long[] TextureData = [];
        public List<string> Pool = [];
        public Dictionary<string, long> StringOffsets = [];
        public List<NameTrie.Node> Nodes = [];
        public RelocationTable.Section[] Sections = [];

        public long[] UserEntries = [];
        public long[] UserDictionaries = [];
        public long[][] UserValues = [];
        public List<NameTrie.Node>[] UserNodes = [];
    }

    private Layout Plan()
    {
        if (Textures.Count == 0)
            throw new InvalidOperationException("A BNTX must contain at least one texture.");

        int count = Textures.Count;
        int alignment = 1 << AlignmentShift;
        Layout layout = new();

        List<string> names = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (BntxTexture texture in Textures)
        {
            if (string.IsNullOrEmpty(texture.Name))
                throw new InvalidOperationException("Every texture must have a name.");
            if (texture.MipCount == 0)
                throw new InvalidOperationException($"Texture '{texture.Name}' has no mip levels.");
            if (!seen.Add(texture.Name))
                throw new InvalidOperationException(
                    $"Two textures are named '{texture.Name}'. Names are the file's lookup keys and must be unique.");

            HashSet<string> entryNames = new(StringComparer.Ordinal);
            foreach (BntxUserData entry in texture.UserData)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    throw new InvalidOperationException(
                        $"Texture '{texture.Name}' has a user data entry with no name.");
                if (!entryNames.Add(entry.Name))
                    throw new InvalidOperationException(
                        $"Texture '{texture.Name}' has two user data entries named '{entry.Name}'.");
            }

            names.Add(texture.Name);
        }

        layout.Nodes = NameTrie.Build(names);

        HashSet<string> distinct = [.. names, Name];
        foreach (BntxTexture texture in Textures)
        foreach (BntxUserData entry in texture.UserData)
        {
            distinct.Add(entry.Name);
            if (entry.Type == UserDataType.String)
                foreach (string value in entry.Strings)
                    distinct.Add(value);
        }

        layout.Pool = [.. distinct];
        layout.Pool.Sort(NameTrie.PoolOrder.Instance);

        layout.StringBlock = InfoPointerOffset + count * 8L;

        long cursor = layout.StringBlock + BlockHeaderSize + 8;
        foreach (string name in layout.Pool)
        {
            layout.StringOffsets[name] = cursor;
            cursor += 2 + Encoding.UTF8.GetByteCount(name) + 1;
            cursor = SurfaceLayout.AlignUp(cursor, 2);
        }

        layout.DictionaryOffset = SurfaceLayout.AlignUp(cursor, 8);
        layout.FirstTexture = SurfaceLayout.AlignUp(
            layout.DictionaryOffset + 8 + (count + 1) * 16L, 8);

        layout.TexturePositions = new long[count];
        layout.UserEntries = new long[count];
        layout.UserDictionaries = new long[count];
        layout.UserValues = new long[count][];
        layout.UserNodes = new List<NameTrie.Node>[count];

        cursor = layout.FirstTexture;
        for (int i = 0; i < count; i++)
        {
            BntxTexture texture = Textures[i];
            layout.TexturePositions[i] = cursor;
            cursor += TextureMipOffsets + texture.MipCount * 8L;
            layout.UserValues[i] = [];

            if (texture.UserData.Count > 0)
            {
                layout.UserEntries[i] = cursor;
                cursor += texture.UserData.Count * (long)UserDataEntrySize;

                layout.UserValues[i] = new long[texture.UserData.Count];
                for (int e = 0; e < texture.UserData.Count; e++)
                {
                    layout.UserValues[i][e] = cursor;
                    cursor = SurfaceLayout.AlignUp(cursor + texture.UserData[e].ValueSize, 8);
                }

                layout.UserDictionaries[i] = cursor;
                cursor += 8 + (texture.UserData.Count + 1L) * DictionaryNodeSize;
                layout.UserNodes[i] = NameTrie.Build(
                    texture.UserData.Select(static entry => entry.Name).ToList());
            }
        }

        layout.TextureContentEnd = cursor;

        layout.DataBlock = SurfaceLayout.AlignUp(cursor + BlockHeaderSize, alignment) - BlockHeaderSize;
        layout.DataStart = layout.DataBlock + BlockHeaderSize;

        layout.TextureData = new long[count];
        cursor = layout.DataStart;
        for (int i = 0; i < count; i++)
        {
            BntxTexture texture = Textures[i];
            cursor = SurfaceLayout.AlignUp(cursor, Math.Max(1, texture.Alignment));
            layout.TextureData[i] = cursor;
            cursor += texture.Data.Length;
        }

        layout.RelocationOffset = SurfaceLayout.AlignUp(cursor, alignment);
        layout.Sections = BuildRelocations(layout);

        int entries = 0;
        foreach (RelocationTable.Section section in layout.Sections)
            entries += section.Entries.Count;

        layout.FileSize = layout.RelocationOffset
            + RelocationTable.HeaderSize
            + RelocationTable.SectionSize * layout.Sections.Length
            + RelocationTable.EntrySize * entries;

        return layout;
    }

    private RelocationTable.Section[] BuildRelocations(Layout layout)
    {
        int count = Textures.Count;

        RelocationTable.Section header = new()
        {
            Position = 0,
            Size = (uint)layout.TextureContentEnd,
        };

        header.Groups.Add(new RelocationTable.Group(ContainerOffset + 0x08, 1));
        header.Groups.Add(new RelocationTable.Group(ContainerOffset + 0x18, 2));
        header.Groups.Add(new RelocationTable.Group(InfoPointerOffset, count));

        for (int i = 0; i <= count; i++)
            header.Groups.Add(new RelocationTable.Group(layout.DictionaryOffset + 8 + i * 16L + 8, 1));

        for (int i = 0; i < count; i++)
        {
            long position = layout.TexturePositions[i];
            BntxTexture texture = Textures[i];

            header.Groups.Add(new RelocationTable.Group(position + 0x60, 1));
            header.Groups.Add(new RelocationTable.Group(position + 0x68, 1));
            header.Groups.Add(new RelocationTable.Group(position + 0x70, 1));
            if (texture.UserData.Count > 0)
                header.Groups.Add(new RelocationTable.Group(position + 0x78, 1));
            header.Groups.Add(new RelocationTable.Group(position + 0x80, 1));
            header.Groups.Add(new RelocationTable.Group(position + 0x88, 1));

            if (texture.UserData.Count == 0)
                continue;

            header.Groups.Add(new RelocationTable.Group(position + 0x98, 1));

            for (int e = 0; e < texture.UserData.Count; e++)
            {
                header.Groups.Add(new RelocationTable.Group(
                    layout.UserEntries[i] + e * (long)UserDataEntrySize, 2));

                if (texture.UserData[e].Type == UserDataType.String)
                    header.Groups.Add(new RelocationTable.Group(
                        layout.UserValues[i][e], texture.UserData[e].Strings.Length));
            }

            for (int n = 0; n <= texture.UserData.Count; n++)
                header.Groups.Add(new RelocationTable.Group(
                    layout.UserDictionaries[i] + 8 + n * (long)DictionaryNodeSize + 8, 1));
        }

        RelocationTable.Section image = new()
        {
            Position = (uint)layout.DataBlock,
            Size = (uint)(layout.RelocationOffset - layout.DataBlock),
        };

        image.Groups.Add(new RelocationTable.Group(ContainerOffset + 0x10, 1));
        for (int i = 0; i < count; i++)
            image.Groups.Add(new RelocationTable.Group(
                layout.TexturePositions[i] + TextureMipOffsets, Textures[i].MipCount));

        RelocationTable.Encode(header);
        RelocationTable.Encode(image);
        return [header, image];
    }

    private void Write(Span<byte> output, Layout layout)
    {
        int count = Textures.Count;

        "BNTX"u8.CopyTo(output);
        WriteU32(output, 0x08, Version);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(0x0C), 0xFEFF);
        output[0x0E] = AlignmentShift;
        output[0x0F] = TargetAddressSize;
        WriteU32(output, 0x10, (uint)(layout.StringOffsets[Name] + 2));
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(0x14), Flag);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice(0x16), (ushort)layout.StringBlock);
        WriteU32(output, 0x18, (uint)layout.RelocationOffset);
        WriteU32(output, 0x1C, (uint)layout.FileSize);

        "NX  "u8.CopyTo(output.Slice(ContainerOffset));
        WriteU32(output, ContainerOffset + 0x04, (uint)count);
        WriteU64(output, ContainerOffset + 0x08, (ulong)InfoPointerOffset);
        WriteU64(output, ContainerOffset + 0x10, (ulong)layout.DataBlock);
        WriteU64(output, ContainerOffset + 0x18, (ulong)layout.DictionaryOffset);
        WriteU64(output, ContainerOffset + 0x20, (ulong)MemoryPoolOffset);

        for (int i = 0; i < count; i++)
            WriteU64(output, InfoPointerOffset + i * 8L, (ulong)layout.TexturePositions[i]);

        WriteStringBlock(output, layout);
        WriteDictionary(output, layout);

        for (int i = 0; i < count; i++)
            WriteTexture(output, layout, i);

        "BRTD"u8.CopyTo(output.Slice((int)layout.DataBlock));
        WriteU64(output, layout.DataBlock + 8, (ulong)(layout.RelocationOffset - layout.DataBlock));

        for (int i = 0; i < count; i++)
            Textures[i].Data.CopyTo(output.Slice((int)layout.TextureData[i]));

        WriteRelocations(output, layout);
    }

    private void WriteStringBlock(Span<byte> output, Layout layout)
    {
        long position = layout.StringBlock;
        "_STR"u8.CopyTo(output.Slice((int)position));
        WriteU32(output, position + 0x04, (uint)(layout.FirstTexture - position));
        WriteU32(output, position + 0x08, (uint)(layout.FirstTexture - position));
        WriteU64(output, position + 0x10, (ulong)layout.Pool.Count);

        foreach (string name in layout.Pool)
        {
            long at = layout.StringOffsets[name];
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)at), (ushort)bytes.Length);
            bytes.CopyTo(output.Slice((int)at + 2));
        }
    }

    private void WriteDictionary(Span<byte> output, Layout layout) =>
        WriteDictionaryAt(output, layout, layout.DictionaryOffset, Textures.Count, layout.Nodes);

    private static void WriteDictionaryAt(
        Span<byte> output, Layout layout, long position, int count, List<NameTrie.Node> nodes)
    {
        "_DIC"u8.CopyTo(output.Slice((int)position));
        WriteU32(output, position + 0x04, (uint)count);

        for (int i = 0; i < nodes.Count; i++)
        {
            NameTrie.Node node = nodes[i];
            long at = position + 8 + i * 16L;
            WriteU32(output, at, unchecked((uint)node.Reference));
            BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)at + 4), node.Left);
            BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)at + 6), node.Right);

            long name = i == 0
                ? layout.StringOffsets[layout.Pool[0]] - 4
                : layout.StringOffsets[node.Key];

            WriteU64(output, at + 8, (ulong)name);
        }
    }

    private void WriteTexture(Span<byte> output, Layout layout, int index)
    {
        BntxTexture texture = Textures[index];
        long position = layout.TexturePositions[index];
        bool last = index == Textures.Count - 1;

        long size = last
            ? layout.DataBlock - position
            : layout.TexturePositions[index + 1] - position;

        "BRTI"u8.CopyTo(output.Slice((int)position));
        WriteU32(output, position + 0x04, (uint)size);
        WriteU32(output, position + 0x08, (uint)size);

        output[(int)position + 0x10] = texture.Flags;
        output[(int)position + 0x11] = unchecked((byte)texture.Dim);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)position + 0x12), (ushort)texture.TileMode);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)position + 0x14), texture.Swizzle);
        BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)position + 0x16), (ushort)texture.MipCount);
        WriteU32(output, position + 0x18, texture.SampleCount);
        WriteU32(output, position + 0x1C, (uint)texture.Format);
        WriteU32(output, position + 0x20, (uint)texture.AccessFlags);
        WriteU32(output, position + 0x24, (uint)texture.Width);
        WriteU32(output, position + 0x28, (uint)texture.Height);
        WriteU32(output, position + 0x2C, (uint)texture.Depth);
        WriteU32(output, position + 0x30, (uint)texture.ArrayLength);
        WriteU32(output, position + 0x34, texture.TextureLayout);
        WriteU32(output, position + 0x38, texture.TextureLayout2);
        WriteU32(output, position + 0x50, (uint)texture.Data.Length);
        WriteU32(output, position + 0x54, texture.Alignment);

        for (int channel = 0; channel < 4 && channel < texture.ChannelTypes.Length; channel++)
            output[(int)position + 0x58 + channel] = (byte)texture.ChannelTypes[channel];

        WriteU32(output, position + 0x5C, (uint)texture.SurfaceDim);

        WriteU64(output, position + 0x60, (ulong)layout.StringOffsets[texture.Name]);
        WriteU64(output, position + 0x68, ContainerOffset);
        WriteU64(output, position + 0x70, (ulong)(position + TextureMipOffsets));
        WriteU64(output, position + 0x80, (ulong)(position + TextureHeaderSize));
        WriteU64(output, position + 0x88, (ulong)(position + TextureHeaderSize + TextureScratchSize));

        for (int level = 0; level < texture.MipCount; level++)
            WriteU64(output, position + TextureMipOffsets + level * 8L,
                (ulong)(layout.TextureData[index] + texture.MipOffsets[level]));

        if (texture.UserData.Count > 0)
            WriteUserData(output, layout, index);
    }

    private void WriteUserData(Span<byte> output, Layout layout, int index)
    {
        BntxTexture texture = Textures[index];
        long position = layout.TexturePositions[index];

        WriteU64(output, position + 0x78, (ulong)layout.UserEntries[index]);
        WriteU64(output, position + 0x98, (ulong)layout.UserDictionaries[index]);

        for (int e = 0; e < texture.UserData.Count; e++)
        {
            BntxUserData entry = texture.UserData[e];
            long at = layout.UserEntries[index] + e * (long)UserDataEntrySize;
            long values = layout.UserValues[index][e];

            WriteU64(output, at, (ulong)layout.StringOffsets[entry.Name]);
            WriteU64(output, at + 0x08, (ulong)values);
            WriteU32(output, at + 0x10, (uint)entry.Count);
            WriteU32(output, at + 0x14, (uint)entry.Type);

            switch (entry.Type)
            {
                case UserDataType.Int32:
                    for (int i = 0; i < entry.Integers.Length; i++)
                        WriteU32(output, values + i * 4, unchecked((uint)entry.Integers[i]));
                    break;

                case UserDataType.Single:
                    for (int i = 0; i < entry.Singles.Length; i++)
                        WriteU32(output, values + i * 4,
                            unchecked((uint)BitConverter.SingleToInt32Bits(entry.Singles[i])));
                    break;

                case UserDataType.Byte:
                    entry.Bytes.CopyTo(output.Slice((int)values));
                    break;

                case UserDataType.String:
                    for (int i = 0; i < entry.Strings.Length; i++)
                        WriteU64(output, values + i * 8, (ulong)layout.StringOffsets[entry.Strings[i]]);
                    break;

                default:
                    throw new NotSupportedException(
                        $"User data entry '{entry.Name}' has type {entry.Type}, whose layout is not established.");
            }
        }

        WriteDictionaryAt(
            output, layout, layout.UserDictionaries[index], texture.UserData.Count, layout.UserNodes[index]);
    }

    private static void WriteRelocations(Span<byte> output, Layout layout)
    {
        long position = layout.RelocationOffset;
        "_RLT"u8.CopyTo(output.Slice((int)position));
        WriteU32(output, position + 0x04, (uint)position);
        WriteU32(output, position + 0x08, (uint)layout.Sections.Length);

        long sectionAt = position + RelocationTable.HeaderSize;
        long entryAt = sectionAt + RelocationTable.SectionSize * layout.Sections.Length;
        int index = 0;

        foreach (RelocationTable.Section section in layout.Sections)
        {
            WriteU32(output, sectionAt + 0x08, section.Position);
            WriteU32(output, sectionAt + 0x0C, section.Size);
            WriteU32(output, sectionAt + 0x10, (uint)index);
            WriteU32(output, sectionAt + 0x14, (uint)section.Entries.Count);
            sectionAt += RelocationTable.SectionSize;

            foreach (RelocationTable.Entry entry in section.Entries)
            {
                WriteU32(output, entryAt, entry.Position);
                BinaryPrimitives.WriteUInt16LittleEndian(output.Slice((int)entryAt + 4), entry.StructCount);
                output[(int)entryAt + 6] = entry.OffsetCount;
                output[(int)entryAt + 7] = entry.PaddingCount;
                entryAt += RelocationTable.EntrySize;
                index++;
            }
        }
    }


    private static bool Matches(ReadOnlySpan<byte> data, long position, string magic)
    {
        if (position < 0 || position + magic.Length > data.Length)
            return false;

        for (int i = 0; i < magic.Length; i++)
            if (data[(int)position + i] != (byte)magic[i])
                return false;

        return true;
    }

    private static string ReadStringAt(ReadOnlySpan<byte> data, long position)
    {
        if (position < 0 || position + 2 > data.Length)
            throw new InvalidDataException($"String offset 0x{position:X} is outside the file.");

        int length = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice((int)position));
        if (position + 2 + length > data.Length)
            throw new InvalidDataException($"String at 0x{position:X} runs past the end of the file.");

        return Encoding.UTF8.GetString(data.Slice((int)position + 2, length));
    }

    private static uint ReadU32(ReadOnlySpan<byte> data, long at) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice((int)at));

    private static ulong ReadU64(ReadOnlySpan<byte> data, long at) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data.Slice((int)at));

    private static void WriteU32(Span<byte> data, long at, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.Slice((int)at), value);

    private static void WriteU64(Span<byte> data, long at, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.Slice((int)at), value);
}
