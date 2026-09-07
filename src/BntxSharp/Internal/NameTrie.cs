using System;
using System.Collections.Generic;
using System.Text;

namespace BntxSharp.Internal;

internal static class NameTrie
{
    public const int RootReference = -1;

    internal struct Node
    {
        public int Reference;
        public ushort Left;
        public ushort Right;
        public string Key;
    }

    public static int BitOf(ReadOnlySpan<byte> name, int index)
    {
        if (index < 0)
            return 0;

        int byteIndex = index >> 3;
        if (byteIndex >= name.Length)
            return 0;

        return (name[name.Length - 1 - byteIndex] >> (index & 7)) & 1;
    }

    public static byte[] Encode(string name) => Encoding.UTF8.GetBytes(name);

    public sealed class PoolOrder : IComparer<string>
    {
        public static readonly PoolOrder Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            byte[] a = Encode(x);
            byte[] b = Encode(y);
            int bits = (Math.Max(a.Length, b.Length) + 1) * 8;

            for (int i = 0; i < bits; i++)
            {
                int difference = BitOf(a, i) - BitOf(b, i);
                if (difference != 0)
                    return difference;
            }

            return string.CompareOrdinal(x, y);
        }
    }

    public static List<Node> Build(IReadOnlyList<string> names)
    {
        List<Node> nodes = [new Node { Reference = RootReference, Left = 0, Right = 0, Key = string.Empty }];

        foreach (string name in names)
        {
            byte[] key = Encode(name);
            ushort added = (ushort)nodes.Count;

            int index = nodes[0].Left;
            int previous = RootReference;
            while (nodes[index].Reference > previous)
            {
                previous = nodes[index].Reference;
                index = BitOf(key, previous) != 0 ? nodes[index].Right : nodes[index].Left;
            }

            string matched = nodes[index].Key;
            if (string.Equals(matched, name, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Duplicate key '{name}'. The split below scans for the first bit at which two " +
                    "names differ, so two equal names would never terminate.", nameof(names));

            byte[] match = Encode(matched);
            int reference = 0;
            while (BitOf(key, reference) == BitOf(match, reference))
                reference++;

            int parent = 0;
            index = nodes[0].Left;
            previous = RootReference;
            while (nodes[index].Reference > previous && nodes[index].Reference < reference)
            {
                previous = nodes[index].Reference;
                parent = index;
                index = BitOf(key, previous) != 0 ? nodes[index].Right : nodes[index].Left;
            }

            nodes.Add(BitOf(key, reference) != 0
                ? new Node { Reference = reference, Left = (ushort)index, Right = added, Key = name }
                : new Node { Reference = reference, Left = added, Right = (ushort)index, Key = name });

            Node link = nodes[parent];
            if (parent == 0 || BitOf(key, link.Reference) == 0)
                link.Left = added;
            else
                link.Right = added;
            nodes[parent] = link;
        }

        return nodes;
    }
}
