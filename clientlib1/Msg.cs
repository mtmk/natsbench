using System.Text;
using System.Text.RegularExpressions;

namespace client1;

public record struct Msg
{
    public string Subject { get; set; }
    public int Sid { get; set; }
    public string ReplyTo { get; set; }
    public string Payload { get; set; }
    public string[] Headers { get; set; }

    public string DumpHeaders()
    {
        if (Headers == null || Headers.Length == 0) return "";

        var sb = new StringBuilder();
        
        foreach (var header in Headers)
        {
            sb.AppendLine(header);
        }

        return sb.ToString();
    }
    
    public string DumpHeadersParsed()
    {
        if (Headers == null || Headers.Length == 0) return "";

        Match m;
        int code;
        string tag;
        if ((m = Regex.Match(Headers[0], @"^NATS/1\.0\s+(\d+)\s+(.+)\s*$")).Success)
        {
            code = int.Parse(m.Groups[1].Value);
            tag = m.Groups[2].Value;
        }
        else
        {
            return $"Error: Can't Parse headers: {DumpHeaders()}";
        }

        if (code == 100 && tag == "Idle Heartbeat")
        {
            return "💓";
        }

        if (code == 408 && tag == "Request Timeout")
        {
            return "❌";
        }

        return DumpHeaders();
    }
}