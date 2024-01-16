using System.Buffers;
using System.Text;

namespace natsparser;

public static class DebuggingExtensions
{
    public static string Dump(this ReadOnlySequence<byte> buffer)
    {
        var sb = new StringBuilder();
        foreach (var readOnlyMemory in buffer)
        {
            sb.Append(Dump(readOnlyMemory.Span));
        }

        return sb.ToString();
    }

    public static string Dump(this ReadOnlySpan<byte> span)
    {
        var sb = new StringBuilder();
        foreach (char b in span)
        {
            switch (b)
            {
                case >= ' ' and <= '~':
                    sb.Append(b);
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                default:
                    sb.Append('.');
                    break;
            }
        }

        return sb.ToString();
    }
}