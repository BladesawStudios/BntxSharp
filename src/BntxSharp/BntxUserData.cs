using System;

namespace BntxSharp;

public enum UserDataType : uint
{
    Int32 = 0,
    Single = 1,
    String = 2,
    Byte = 3,
    WString = 4,
}

public sealed class BntxUserData
{
    public string Name { get; set; } = string.Empty;
    public UserDataType Type { get; set; }
    public int[] Integers { get; set; } = [];
    public float[] Singles { get; set; } = [];
    public byte[] Bytes { get; set; } = [];
    public string[] Strings { get; set; } = [];

    public int Count => Type switch
    {
        UserDataType.Int32 => Integers.Length,
        UserDataType.Single => Singles.Length,
        UserDataType.Byte => Bytes.Length,
        UserDataType.String => Strings.Length,
        _ => 0,
    };

    public long ValueSize => Type switch
    {
        UserDataType.Int32 => Integers.Length * 4L,
        UserDataType.Single => Singles.Length * 4L,
        UserDataType.Byte => Bytes.Length,
        UserDataType.String => Strings.Length * 8L,
        _ => throw new NotSupportedException($"User data type {Type} has no known layout."),
    };
}
