

using System.Text;
using ObjectLayoutInspector;

ReadOnlySpan<char> span = "123".AsSpan();
char c = span[0];
var rune = new Rune(c);
var runeValue = rune.Value;
byte b = (byte)c;
Encoding.ASCII.GetBytes(span.ToArray());

//TypeLayout.PrintLayout<Message>();
// TypeLayout.PrintLayout<StructWithFixedBuffer>(recursively: true);
// TypeLayout.PrintLayout<NatsKey>(recursively: true);
// TypeLayout.PrintLayout<NotAlignedStruct>(recursively: true);
// TypeLayout.PrintLayout<ClassWithNestedCustomStruct>(recursively: true);

var r = new R { S = "1234" };

Console.WriteLine(r);

public record R
{
    private readonly string _s = String.Empty;

    public string S
    {
        get => _s;
        init
        {
            _s = value;
            N = int.Parse(_s);
        }
    }

    internal int N { get; init; }
}


public struct Message
{
    public NatsKey Subject;
    public NatsKey ReplyTo;
}

public unsafe struct StructWithFixedBuffer
{
    public fixed char Text[128];
    public int F2;
    // other fields...
}

public struct NotAlignedStruct
{
    public byte m_byte1;
    public int m_int;

    public byte m_byte2;
    public short m_short;
}

public class ClassWithNestedCustomStruct
{
    public byte b;
    public NotAlignedStruct sp1;
}

public readonly struct NatsKey : IEquatable<NatsKey>
{
    public readonly string Key;
    internal readonly byte[]? Buffer; // subject with space padding.

    public NatsKey(string key)
        : this(key, false)
    {
    }

    internal NatsKey(string key, bool withoutEncoding)
    {
        Key = key;
        if (withoutEncoding)
        {
            Buffer = null;
        }
        else
        {
            Buffer = Encoding.ASCII.GetBytes(key + " ");
        }
    }

    internal int LengthWithSpacePadding => Key.Length + 1;

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }

    public bool Equals(NatsKey other)
    {
        return Key == other.Key;
    }

    public override string ToString()
    {
        return Key;
    }
}