

using System.Text;
using ObjectLayoutInspector;

TypeLayout.PrintLayout<Struct8>();
// TypeLayout.PrintLayout<NatsJSControlMsg<R>>();

// ReadOnlySpan<char> span = "123".AsSpan();
// char c = span[0];
// var rune = new Rune(c);
// var runeValue = rune.Value;
// byte b = (byte)c;
// Encoding.ASCII.GetBytes(span.ToArray());

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

internal enum NatsJSControlMsgType
{
    None,
    Heartbeat,
    Timeout,
}

internal readonly struct NatsJSControlMsg<T>
{
    public NatsJSMsg<T?> JSMsg { get; init; }

    public bool IsControlMsg => ControlMsgType != NatsJSControlMsgType.None;

    public NatsJSControlMsgType ControlMsgType { get; init; }
}


public readonly struct NatsJSMsg<T>
{
    public NatsMsg<T> Msg { get; init; }
}

public readonly record struct NatsMsg<T>(
    string Subject,
    string? ReplyTo,
    int Size,
    NatsHeadersX? Headers,
    T? Data,
    INatsConnectionX? Connection);

public interface INatsConnectionX
{
}

public class NatsHeadersX
{
}

record struct Struct8(long F1, long F2, long F3, long F4, long F5, long F6, long F7, long F8);
