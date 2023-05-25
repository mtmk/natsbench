using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace client1;

public record struct Msg
{
    public string Subject { get; set; }
    public int Sid { get; set; }
    public string ReplyTo { get; set; }
    public string Payload { get; set; }
}

public class Program
{
    static void Main()
    {
        var client = new TcpClient();
        client.Connect("localhost", 4222);
        var stream = client.GetStream();
        var sr = new StreamReader(stream, Encoding.ASCII);
        var sw = new StreamWriter(stream);

        void Log(string name, object? obj)
        {
            var color = Console.ForegroundColor;
            if (name == "RX") Console.ForegroundColor = ConsoleColor.Green;
            if (name == "TX") Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine($"[{name}] {obj}");
            Console.ForegroundColor = color;
        }
        
        string? RcvLine()
        {
            var line = sr.ReadLine();
            Log("RX", line);
            return line;
        }

        void SendLine(string line)
        {
            Log("TX", line);
            sw.WriteLine(line);
            sw.Flush();
        }

        var writers = new ConcurrentDictionary<int, ChannelWriter<Msg>>();
        int sid = 0;
        void Sub(string subject, string queueGroup, Action<Msg> action)
        {
            sid++;
            SendLine($"SUB {subject} {queueGroup} {sid}");
            var channel = Channel.CreateUnbounded<Msg>();
            ChannelWriter<Msg> writer = channel.Writer;
            writers[sid] = writer;
            Task.Run(async delegate
            {
                await foreach (var msg in channel.Reader.ReadAllAsync())
                {
                    action(msg);
                }
            });
        }

        void Pub(string subject, string replyTo, string payload)
        {
            SendLine($"PUB {subject} {replyTo} {payload.Length}");
            SendLine(payload);
        }
        
        var connected = new ManualResetEventSlim();
        Task.Run(delegate
        {
            while (RcvLine() is { } line)
            {
                if (line.StartsWith("INFO"))
                {
                    SendLine("CONNECT {}");
                    connected.Set();
                }
                else if (line.StartsWith("PING"))
                {
                    SendLine("PONG");
                }
                else if (line.StartsWith("MSG"))
                {
                    var match = Regex.Match(line, @"^MSG\s+(\S+)\s+(\S+)\s+(?:(\S+)\s+)?(\d+)");
                    if (!match.Success) throw new Exception("MSG ERR");
                    var subject = match.Groups[1].Value;
                    var sid = int.Parse(match.Groups[2].Value);
                    var replyTo = match.Groups[3].Value;
                    var size = int.Parse(match.Groups[4].Value);
                    var buffer = new char[size + 2];
                    var span = buffer.AsSpan();
                    while (true)
                    {
                        var read = sr.Read(span);
                        if (read == -1) throw new Exception("READ CLOSED");
                        if (read == 0) break;
                        span = span[read..];
                    }

                    var payload = new string(buffer.AsSpan()[..size]);
                    Log("RX", payload);
                    var msg = new Msg
                    {
                        Subject = subject,
                        Sid = sid,
                        Payload = payload,
                        ReplyTo = replyTo
                    };
                    writers[sid].TryWrite(msg);
                }
                
            }
        });
        connected.Wait();
        
        while (true)
        {
            var cmd = Console.ReadLine();
            if (cmd == null || cmd.StartsWith("q")) break;
            if (cmd.StartsWith("pub"))
            {
                var match = Regex.Match(cmd, @"^pub\s+(\S+)\s+(?:(\S+)\s+)?(\S+)$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var replyTo = match.Groups[2].Value;
                var payload = match.Groups[3].Value;
                Console.WriteLine($"Publish to {subject}");
                Pub(subject, replyTo, payload);
            }
            else if (cmd.StartsWith("sub"))
            {
                var match = Regex.Match(cmd, @"^sub\s+(\S+)(?:\s+(\S+))?$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var queueGroup = match.Groups[2].Value;
                Console.WriteLine($"Subscribe to {subject}");
                Sub(subject, queueGroup, m =>
                {
                    Console.WriteLine($"Message received ({queueGroup}): {m}");
                    if (!string.IsNullOrEmpty(m.ReplyTo))
                    {
                        Pub(m.ReplyTo, "", $"Got your message '{m.Payload}'");
                    }
                });
            }
            else
            {
                Console.WriteLine("Unknown command");
            }
        }
        
        client.Close();
    }
}