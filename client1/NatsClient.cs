using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace client1;

public class NatsClient
{
    private readonly string _url;
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly StreamReader _sr;
    private readonly StreamWriter _sw;
    private readonly ConcurrentDictionary<int,ChannelWriter<Msg>> _writers;
    private volatile int _sid;
    private volatile bool _logCtrl;

    private NatsClient(
        string url,
        TcpClient client,
        NetworkStream stream,
        StreamReader sr,
        StreamWriter sw,
        ConcurrentDictionary<int, ChannelWriter<Msg>> writers,
        int sid,
        bool logCtrl)
    {
        _url = url;
        _client = client;
        _stream = stream;
        _sr = sr;
        _sw = sw;
        _writers = writers;
        _sid = sid;
        _logCtrl = logCtrl;
    }

    public static NatsClient Connect(bool logCtrl = false)
    {
        var client = new TcpClient();

        var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "localhost:4222";
        
        client.Connect(url.Split(':')[0], int.Parse(url.Split(':')[1]));
        
        var stream = client.GetStream();
        var sr = new StreamReader(stream, Encoding.ASCII);
        var sw = new StreamWriter(stream);

        var writers = new ConcurrentDictionary<int, ChannelWriter<Msg>>();
        var sid = 0;

        return new NatsClient(url, client, stream, sr, sw, writers, sid, logCtrl).Start();
    }

    NatsClient Start()
    {
        var connected = new ManualResetEventSlim();
        Task.Run(delegate
        {
            while (RcvLine() is { } line)
            {
                if (line.StartsWith("INFO"))
                {
                    SendLine("CONNECT {\"headers\":true}");
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
                        var read = _sr.Read(span);
                        if (read == -1) throw new Exception("READ CLOSED");
                        if (read == 0) break;
                        span = span[read..];
                    }

                    var payload = new string(buffer.AsSpan()[..size]);
                    Log("RX", payload, noTagIndent:true);
                    var msg = new Msg
                    {
                        Subject = subject,
                        Sid = sid,
                        Payload = payload,
                        ReplyTo = replyTo
                    };
                    _writers[sid].TryWrite(msg);
                }
                else if (line.StartsWith("HMSG"))
                {
                    // https://docs.nats.io/reference/reference-protocols/nats-protocol#hmsg
                    // HMSG <subject> <sid> [reply-to] <#header bytes> <#total bytes>␍␊[headers]␍␊␍␊[payload]␍␊
                    var match = Regex.Match(line, @"^HMSG\s+(\S+)\s+(\S+)\s+(?:(\S+)\s+)?(\d+)\s+(\d+)");
                    if (!match.Success) throw new Exception("HMSG ERR");
                    var subject = match.Groups[1].Value;
                    var sid = int.Parse(match.Groups[2].Value);
                    var replyTo = match.Groups[3].Value;
                    var headersSize = int.Parse(match.Groups[4].Value);
                    var size = int.Parse(match.Groups[5].Value);
                    
                    // slurp headers
                    var headers = new List<string>();
                    while (true)
                    {
                        var header = _sr.ReadLine();
                        if (header == null) throw new Exception("HMSG HEADERS ERR");;
                        if (header == string.Empty) break;
                        headers.Add(header);
                        Log("RX", header, noTagIndent:true);
                    }

                    var buffer = new char[size - headersSize + 2];
                    var span = buffer.AsSpan();
                    while (true)
                    {
                        var read = _sr.Read(span);
                        if (read == -1) throw new Exception("READ CLOSED");
                        if (read == 0) break;
                        span = span[read..];
                    }

                    var payload = new string(buffer.AsSpan()[..(size - headersSize)]);
                    Log("RX", payload, noTagIndent:true);
                    var msg = new Msg
                    {
                        Subject = subject,
                        Sid = sid,
                        Payload = payload,
                        ReplyTo = replyTo,
                        Headers = headers.ToArray(),
                    };
                    _writers[sid].TryWrite(msg);
                }
            }
        });
        connected.Wait();
        
        Log("ii", $"Connected to {_url}");

        return this;
    }

    public void Close()
    {
        _stream.Close();
        _client.Close();
    }

    [Conditional("DEBUG")]
    public void LogLine(string? message) => Log("NA", message, false, "\n", false);
    
    [Conditional("DEBUG")]
    public void Log(string? message) => Log("NA", message, false, "", false);
    
    [Conditional("DEBUG")]
    public void Log(string name, string? message, bool noTagIndent = false) => Log(name, message, true, "\n", noTagIndent);
    
    [Conditional("DEBUG")]
    public void Log(string name, string? message, bool tag, string suffix, bool noTagIndent)
    {
        if (!_logCtrl
            && message != null
            && Regex.IsMatch(message, @"^(?:PING|PONG|CONNECT|INFO|\+OK)"))
        {
            return;
        }

        lock (this)
        {
            var color = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = name switch
                {
                    "ii" => ConsoleColor.DarkGray,
                    "RX" => ConsoleColor.Green,
                    "TX" => ConsoleColor.Yellow,
                    _ => color
                };

                if (tag)
                {
                    if (noTagIndent)
                    {
                        //                $"mm:ss.fff [XX] xxxx
                        Console.Out.Write($"               {message}{suffix}");
                    }
                    else
                    {
                        Console.Out.Write($"{DateTime.Now:mm:ss.fff} [{name}] {message}{suffix}");
                    }
                }
                else
                {
                    Console.Out.Write($"{message}{suffix}");
                }
            }
            finally
            {
                Console.ForegroundColor = color;
            }
        }
    }
    
    string? RcvLine()
    {
        var line = _sr.ReadLine();
        Log("RX", line);
        return line;
    }

    void SendLine(string line)
    {
        Log("TX", line);
        _sw.WriteLine(line);
        _sw.Flush();
    }

    public void Ping()
    {
        SendLine("PING");
    }
    
    public int Sub(string subject, string queueGroup, Action<Msg> action)
    {
        var sid = Interlocked.Increment(ref _sid);
        SendLine($"SUB {subject} {queueGroup} {sid}");
        var channel = Channel.CreateUnbounded<Msg>();
        ChannelWriter<Msg> writer = channel.Writer;
        _writers[sid] = writer;
        Task.Run(async delegate
        {
            await foreach (var msg in channel.Reader.ReadAllAsync())
            {
                action(msg);
            }
        });
        return sid;
    }

    public void UnSub(int sid, int? max)
    {
        SendLine($"UNSUB {sid} {max}");
    }

    public void Pub(string subject, string replyTo, string payload)
    {
        SendLine($"PUB {subject} {replyTo} {payload.Length}");
        SendLine(payload);
    }
    
    /// <summary>
    /// https://docs.nats.io/reference/reference-protocols/nats-protocol#hpub
    /// HPUB subject [reply-to] #header-bytes #total-bytes\r\n[headers]\r\n\r\n[payload]\r\n
    /// #header bytes: The size of the headers section in bytes including the ␍␊␍␊ delimiter before the payload.
    /// #total bytes: The total size of headers and payload sections in bytes.
    /// </summary>
    /// <param name="subject"></param>
    /// <param name="replyTo"></param>
    /// <param name="payload"></param>
    public void HPub(string subject, string replyTo, string payload, string headersString)
    {
        var headers = new List<string>();
        
        headers.Add("NATS/1.0");
        
        foreach (var h in headersString.Split(','))
        {
            var kv = h.Split(':');
            var k = kv.Length > 0 ? kv[0] : "";
            var v = kv.Length > 1 ? kv[1] : "";
            headers.Add($"{k}: {v}");
        }

        headers.Add("");

        var headersOutput = string.Join("\n\r", headers) + "\n\r";

        SendLine($"HPUB {subject} {replyTo} {headersOutput.Length} {headersOutput.Length + payload.Length}");
        foreach (var header in headers)
        {
            SendLine(header);
        }
        SendLine(payload);
    }

    public bool Ctrl => _logCtrl;
    
    public void CtrlOn() => _logCtrl = true;
    
    public void CtrlOff() => _logCtrl = false;
}