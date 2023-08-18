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

    public readonly string DumpHeaders()
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

        string Error(in Msg self, string error)
        {
            return $"Error: {error}: {self.DumpHeaders()}";
        }
        
        int code;
        string tag;
        {
            Match m;
            if ((m = Regex.Match(Headers[0], @"^NATS/1\.0\s+(\d+)\s+(.+)\s*$")).Success)
            {
                code = int.Parse(m.Groups[1].Value);
                tag = m.Groups[2].Value;
            }
            else
            {
                return $"Error: Can't Parse headers: {DumpHeaders()}";
            }
        }
        
        int? pendingMsgs = default;
        int? pendingBytes = default;
        int? lastStream = default;
        int? lastConsumer = default;
        foreach (var header in Headers)
        {
            // Nats-Pending-Messages: 15\r\nNats-Pending-Bytes
            // Nats-Last-Consumer: 23\r\nNats-Last-Stream: 20
            Match m;
            if ((m = Regex.Match(header, @"^\s*Nats-(\w+)-(\w+):\s*(\d+)\s*$")).Success)
            {
                var what = m.Groups[1].Value;
                var type = m.Groups[2].Value;
                var size = int.Parse(m.Groups[3].Value);
                if (what == "Pending" && type == "Messages")
                {
                    pendingMsgs = size;
                }
                else if (what == "Pending" && type == "Bytes")
                {
                    pendingBytes = size;
                }
                else if (what == "Last" && type == "Stream")
                {
                    lastStream = size;
                }
                else if (what == "Last" && type == "Consumer")
                {
                    lastConsumer = size;
                }
                else
                {
                    return Error(this, $"Can't parse header '{type}'");
                }
            }
        }

        if (code == 100 && tag == "Idle Heartbeat")
        {
            return $"💓 last stream:{lastStream} consumer:{lastConsumer}";
        }

        if (code == 408 && tag == "Request Timeout")
        {
            return $"❌ pending msgs:{pendingMsgs} bytes:{pendingBytes}";
        }

        return DumpHeaders();
    }
}