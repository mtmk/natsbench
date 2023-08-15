using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

#pragma warning disable CS4014
public class Server
{
    public class AtomicBool
    {
        private int _i;

        public AtomicBool(bool value) => _i = value ? 1 : 0;

        public void Set() => Interlocked.Exchange(ref _i, 1);
        
        public void Reset() => Interlocked.Exchange(ref _i, 0);

        public void Toggle()
        {
            if (True) Reset();
            else Set();
        }

        public bool True => Volatile.Read(ref _i) == 1;
        
        public static implicit operator bool(AtomicBool d) => d.True;
        
        public static explicit operator AtomicBool(bool b) => new(b);

        public override string ToString() => True ? "ON" : "OFF";
    }
    
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();

    public AtomicBool JetStreamSummarization { get; } = new(false);
    public AtomicBool SuppressHeartbeats { get; } = new(false);
    public AtomicBool DisplayMessages { get; } = new(true);
    public AtomicBool DisplayHeartbeats { get; } = new(true);
    public AtomicBool DisplayCtrl { get; } = new(true);

    public void NewClient(int id, TcpClient tcpClient)
    {
        _clients[id] = tcpClient;
    }

    public void Drop(int id)
    {
        Console.WriteLine($"Dropping client [{id}]...");
        if (_clients.TryGetValue(id, out var client))
        {
            try
            {
                client.Close();
                Console.WriteLine($"Client [{id}] connection closed.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: closing client [{id}]: {e.GetBaseException().Message}");
            }
        }
        else
        {
            Console.WriteLine($"Error: Can't find client [{id}]");
        }
    }

    public void Print(string dir, string line, string ascii)
    {
        try
        {
            if (JetStreamSummarization)
            {
                string? js = null;
                var skip = false;
                Match m1;
                Match m2;
                if ((m1 = Regex.Match(ascii, @"100 Idle Heartbeat\\r\\nNats-Last-Consumer: (\d+)\\r\\nNats-Last-Stream: (\d+)")).Success)
                {
                    if (DisplayHeartbeats)
                    {
                        // NATS/1.0 100 Idle Heartbeat\r\nNats-Last-Consumer: 107\r\nNats-Last-Stream: 107\r\n\r\n
                        var lastConsumer = m1.Groups[1].Value;
                        var lastStream = m1.Groups[2].Value;
                        js = $"[C] HBT consumer:{lastConsumer} stream:{lastStream}";
                    }
                    else
                    {
                        js = "";
                        skip = true;
                    }
                }
                else if ((m1 = Regex.Match(ascii, @"408 Request Timeout\\r\\nNats-Pending-Messages: (\d+)\\r\\nNats-Pending-Bytes: (\d+)")).Success)
                {
                    // NATS/1.0 408 Request Timeout\r\nNats-Pending-Messages: 10\r\nNats-Pending-Bytes: 0\r\n\r\n
                    var pendingMsgs = m1.Groups[1].Value;
                    var pendingBytes = m1.Groups[2].Value;
                    js = $"[C] RTO pending msgs:{pendingMsgs} bytes:{pendingBytes}";
                }
                else if ((m1 = Regex.Match(ascii, @"409 Message Size Exceeds MaxBytes\\r\\nNats-Pending-Messages: (\d+)\\r\\nNats-Pending-Bytes: (\d+)")).Success)
                {
                    // HMSG _INBOX.AZ6BFWO00BKRERV8NM9LS9 2 97 97
                    // NATS/1.0 409 Message Size Exceeds MaxBytes\r\nNats-Pending-Messages: 99\r\nNats-Pending-Bytes: 46\r\n\r\n
                    var pendingMsgs = m1.Groups[1].Value;
                    var pendingBytes = m1.Groups[2].Value;
                    js = $"[C] EXB pending msgs:{pendingMsgs} bytes:{pendingBytes}";
                }
                
                else if ((m1 = Regex.Match(line, @"^PUB \$JS\.API\.CONSUMER\.MSG\.NEXT\.(\w+)\.(\w+)")).Success)
                {
                    // PUB $JS.API.CONSUMER.MSG.NEXT.s1.c2 _INBOX.82YIGQ8W3TI0XNGETMHF03 77
                    // {"batch":10,"max_bytes":0,"idle_heartbeat":15000000000,"expires":30000000000}
                    var stream = m1.Groups[1].Value;
                    var consumer = m1.Groups[2].Value;
                    var json = JsonNode.Parse(ascii);
                    var batch = json["batch"]?.GetValue<int>();
                    var maxBytes = json["max_bytes"]?.GetValue<int>();
                    js = $"[C] NXT {stream}/{consumer} batch:{batch} max_bytes:{maxBytes}";
                }
                else if (
                    (m1 = Regex.Match(line, @"^PUB \$JS\.ACK\.(\w+)\.(\w+)\.(\d+)\.(\d+)\.(\d+)\.(\d+)\.(\d+)")).Success
                    && (m2 = Regex.Match(ascii, @"^\+ACK$")).Success)
                {
                    if (DisplayMessages.True)
                    {
                        // PUB $JS.ACK.s1.c2.1.119.119.1692099975248777900.0 4
                        //+ACK
                        var stream = m1.Groups[1].Value;
                        var consumer = m1.Groups[2].Value;
                        var v1 = ulong.Parse(m1.Groups[3].Value);
                        var v2 = ulong.Parse(m1.Groups[4].Value);
                        var v3 = ulong.Parse(m1.Groups[5].Value);
                        var v4 = ulong.Parse(m1.Groups[6].Value);
                        var v5 = ulong.Parse(m1.Groups[7].Value);
                        js = $"[M] ACK {stream}/{consumer} v1:{v1} v2:{v2} v3:{v3} v4:{v4} v5:{v5}";
                    }
                    else
                    {
                        js = "";
                        skip = true;
                    }
                }
                else if ((m1 = Regex.Match(line, @"^MSG (\S+) (\S+) \$JS\.ACK\.(\w+)\.(\w+)\.(\d+)\.(\d+)\.(\d+)\.(\d+)\.(\d+)")).Success)
                {
                    if (DisplayMessages)
                    {
                        // MSG s1.x 2 $JS.ACK.s1.c2.1.130.130.1692100700116417900.0 17
                        // AAAAAAAAAAAAAAAAA
                        var subject = m1.Groups[1].Value;
                        var sid = m1.Groups[2].Value;
                        var stream = m1.Groups[3].Value;
                        var consumer = m1.Groups[4].Value;
                        var v1 = ulong.Parse(m1.Groups[5].Value);
                        var v2 = ulong.Parse(m1.Groups[6].Value);
                        var v3 = ulong.Parse(m1.Groups[7].Value);
                        var v4 = ulong.Parse(m1.Groups[8].Value);
                        var v5 = ulong.Parse(m1.Groups[9].Value);
                        js = $"[M] MSG {subject} {stream}/{consumer} v1:{v1} v2:{v2} v3:{v3} v4:{v4} v5:{v5}"
                            // + $"\n    {new string(' ', dir.Length)} {ascii}"
                            ;
                    }
                    else
                    {
                        js = "";
                        skip = true;
                    }
                }
                else if ((m1 = Regex.Match(ascii, @"\{""type"":""io\.nats\.jetstream\.api\.v1\.([\w.]+)"",")).Success)
                {
                    //[9] <-- MSG _INBOX.W4KLXR004L8LA1ZT0DISVI.W4KLXR004L8LA1ZT0DISWW 1 550
                    //{"type":"io.nats.jetstream.api.v1.consumer_info_response",...
                    var type = m1.Groups[1].Value;
                    var json = JsonNode.Parse(ascii);
                    var error = json["error"]?.ToString();
                    js = $"[C] API Response {type} {error}";
                }
                else if ((m1 = Regex.Match(line, @"^PUB \$JS\.API\.(\S+)")).Success)
                {
                    // PUB $JS.API.CONSUMER.MSG.NEXT.s1.c2 _INBOX.82YIGQ8W3TI0XNGETMHF03 77
                    // {"batch":10,"max_bytes":0,"idle_heartbeat":15000000000,"expires":30000000000}
                    var api = m1.Groups[1].Value;
                    js = $"[C] API Request {api}";
                }
                    
                if (js != null)
                {
                    if (!skip)
                    {
                        Console.WriteLine($"{dir} JS {DateTime.Now:HH:mm:ss} {js}");
                    }

                    return;
                }
            }

            Console.WriteLine($"{dir} {line}\n{new string(' ', dir.Length)} {ascii}\n");
        }
        catch (Exception e)
        {
            Console.WriteLine($"");
            Console.WriteLine($"Print error :{e.Message}");
            Console.WriteLine($"  dir:{dir}");
            Console.WriteLine($"  line:{line}");
            Console.WriteLine($"  buffer:{ascii}");
            Console.WriteLine($"");
        }
    }

    public void Write(TextWriter sw, string dir, string line, char[] buffer, string ascii)
    {
        Print(dir, line, ascii);
        if (SuppressHeartbeats && Regex.IsMatch(ascii, @"100 Idle Heartbeat"))
        {
            if (DisplayHeartbeats)
                Console.WriteLine($"{new string(' ', dir.Length)}     [SUPPRESSED]");
        }
        else
        {
            sw.WriteLine(line);
            sw.Write(buffer);
            sw.Flush();
        }
    }

    public void WriteCtrl(TextWriter sw, string dir, string line)
    {
        // Ignore control messages
        if (DisplayCtrl && !Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|\+OK)"))
        {
            Console.WriteLine($"{dir} {line}");
        }

        sw.WriteLine(line);
        sw.Flush();
    }
}