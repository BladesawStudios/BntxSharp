using System;
using System.Collections.Generic;

namespace BntxSharp.Internal;

internal static class RelocationTable
{
    public const int HeaderSize = 0x10;

    public const int SectionSize = 0x18;

    public const int EntrySize = 8;

    public const int MaxPadding = byte.MaxValue;

    public const int MaxOffsets = byte.MaxValue;

    internal readonly record struct Group(long Position, int Count);

    internal readonly record struct Entry(uint Position, ushort StructCount, byte OffsetCount, byte PaddingCount);

    internal sealed class Section
    {
        public uint Position;
        public uint Size;
        public List<Group> Groups { get; } = [];
        public List<Entry> Entries { get; } = [];
    }

    public static void Encode(Section section)
    {
        section.Entries.Clear();

        List<Group> ordered = [.. section.Groups];
        ordered.Sort(static (a, b) => a.Position.CompareTo(b.Position));

        List<Group> merged = [];
        foreach (Group group in ordered)
        {
            if (merged.Count > 0)
            {
                Group last = merged[^1];
                if (last.Position + last.Count * 8L == group.Position)
                {
                    merged[^1] = last with { Count = last.Count + group.Count };
                    continue;
                }
            }

            merged.Add(group);
        }

        List<Group> groups = [];
        foreach (Group group in merged)
        {
            for (int done = 0; done < group.Count; done += MaxOffsets)
                groups.Add(new Group(
                    group.Position + (long)done * 8,
                    Math.Min(MaxOffsets, group.Count - done)));
        }

        bool[] placed = new bool[groups.Count];

        for (int i = 0; i < groups.Count; i++)
        {
            if (placed[i])
                continue;

            Group first = groups[i];
            placed[i] = true;

            int next = NextMatching(groups, placed, i + 1, first.Count);
            long stride = next >= 0 ? groups[next].Position - first.Position : 0;
            int structCount = 1;

            if (next >= 0 && stride > 0 && FitsPadding(stride, first.Count))
            {
                long expected = groups[next].Position;
                int search = next;

                while (search >= 0 && groups[search].Position == expected)
                {
                    placed[search] = true;
                    structCount++;
                    expected += stride;

                    if (structCount == ushort.MaxValue)
                        break;

                    search = FindAt(groups, placed, expected, first.Count);
                }
            }
            else
            {
                stride = 0;
            }

            int padding = stride > 0 ? (int)(stride / 8) - first.Count : 0;

            section.Entries.Add(new Entry(
                (uint)first.Position,
                (ushort)structCount,
                (byte)first.Count,
                (byte)padding));
        }
    }

    public static List<long> Expand(IEnumerable<Entry> entries)
    {
        List<long> positions = [];

        foreach (Entry entry in entries)
        {
            long stride = (entry.OffsetCount + entry.PaddingCount) * 8L;
            for (int structIndex = 0; structIndex < entry.StructCount; structIndex++)
            for (int offset = 0; offset < entry.OffsetCount; offset++)
                positions.Add(entry.Position + structIndex * stride + offset * 8L);
        }

        return positions;
    }

    private static bool FitsPadding(long stride, int count) =>
        stride % 8 == 0 && stride / 8 - count >= 0 && stride / 8 - count <= MaxPadding;

    private static int NextMatching(List<Group> groups, bool[] placed, int from, int count)
    {
        for (int i = from; i < groups.Count; i++)
            if (!placed[i] && groups[i].Count == count)
                return i;

        return -1;
    }

    private static int FindAt(List<Group> groups, bool[] placed, long position, int count)
    {
        for (int i = 0; i < groups.Count; i++)
            if (!placed[i] && groups[i].Count == count && groups[i].Position == position)
                return i;

        return -1;
    }
}
