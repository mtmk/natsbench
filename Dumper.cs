using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace NATS.Client.Core;

internal static class Dumper
{
    private static readonly bool TraceOn;
    private static readonly bool TimestampOn;
    private static readonly Stopwatch? Timestamper;

    static Dumper()
    {
        _ = int.TryParse(Environment.GetEnvironmentVariable("NATS_CLIENT_PROTOCOL_TRACE") ?? "0", out var trace);

        TraceOn = trace > 0;
        TimestampOn = trace > 1;

        if (TimestampOn)
            Timestamper = Stopwatch.StartNew();
    }

    public static void Dump(string direction, ReadOnlySequence<byte> buffer)
    {
        if (!TraceOn) return;

        var sb = new StringBuilder();
        foreach (var memory in buffer)
        {
            Decode(sb, memory);
        }

        Print(direction, sb);
    }

    public static void Dump(string direction, ReadOnlyMemory<byte> memory)
    {
        if (!TraceOn) return;

        var sb = new StringBuilder();
        Decode(sb, memory);

        Print(direction, sb);
    }

    private static void Decode(StringBuilder sb, ReadOnlyMemory<byte> memory)
    {
        foreach (var b in memory.Span)
        {
            var c = (char)b;
            switch (c)
            {
            case > ' ' and <= '~':
                sb.Append(c);
                break;
            case ' ':
                sb.Append('␠');
                break;
            case '\t':
                sb.Append('␋');
                break;
            case '\n':
                sb.Append('␊');
                break;
            case '\r':
                sb.Append('␍');
                break;
            default:
                sb.Append('.');
                break;
            }
        }
    }

    private static void Print(string direction, StringBuilder sb)
    {
        var time = TimestampOn ? $"{Timestamper?.Elapsed} " : string.Empty;
        Console.Error.WriteLine($"{time}[{direction}] {sb}");
    }
}
