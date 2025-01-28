using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace nnats_proxy;

public class ProxyServer
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
        // if (ascii.Length > 1024)
        // {
        //     ascii = $"{ascii.Substring(0, 1024)}...({ascii.Length})";
        // }
        
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
                // NATS/1.0 409 Server Shutdown\r\nNats-Pending-Messages: 500\r\nNats-Pending-Bytes: 0\r\n\r\n
                    
                //
                else if ((m1 = Regex.Match(line, @"^PUB \$JS\.API\.CONSUMER\.MSG\.NEXT\.(\w+)\.(\w+)")).Success)
                {
                    // PUB $JS.API.CONSUMER.MSG.NEXT.s1.c2 _INBOX.82YIGQ8W3TI0XNGETMHF03 77
                    // {"batch":10,"max_bytes":0,"idle_heartbeat":15000000000,"expires":30000000000}
                    var stream = m1.Groups[1].Value;
                    var consumer = m1.Groups[2].Value;
                    var json = JsonNode.Parse(ascii);
                    var batch = json?["batch"]?.GetValue<int>();
                    var maxBytes = json?["max_bytes"]?.GetValue<int>();
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
                    var error = json?["error"]?.ToString();
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

    public void Write(TextWriter sw, string dir, string line, ReadOnlySpan<char> buffer, string ascii)
    {
        Print(dir, line, ascii);
        if (SuppressHeartbeats && Regex.IsMatch(ascii, @"100 Idle Heartbeat"))
        {
            if (DisplayHeartbeats)
                Console.WriteLine($"{new string(' ', dir.Length)}     [SUPPRESSED]");
        }
        else
        {
            sw.Write(line);
            sw.Write("\r\n");
            sw.Write(buffer);
            sw.Flush();
        }
    }
    
    public void PrintBin(string dir, string line, string ascii)
    {
        try
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {dir} {line}\n{ascii}\n");
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
    
    public void WriteBin(StreamWriter sw, string dir, string line, ReadOnlySpan<char> buffer, string ascii)
    {
        PrintBin(dir, line, ascii);
        sw.Write(buffer);
        sw.Flush();
    }

    public void WriteCtrl(TextWriter sw, string dir, string line)
    {
        // Ignore control messages
        if (DisplayCtrl || !Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|\+OK)"))
        {
            Console.WriteLine($"{dir} {line}");
        }

        sw.Write(line);
        sw.Write("\r\n");
        sw.Flush();
    }
    
    static void SetupSocket(Socket socket)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                socket.SendBufferSize = 0;
                socket.ReceiveBufferSize = 0;
            }
            catch
            {
                /*ignore*/
            }
        }

        socket.NoDelay = true;
    }

    private int _client = 0;
    
    static void RunTCPServer(ProxyServer proxyServer, ManualResetEventSlim started, IPAddress address, int proxyPort, IPAddress serverAddress, int serverPort)
    {
        var tcpListener = new TcpListener(address, proxyPort);

        SetupSocket(tcpListener.Server);

        tcpListener.Start();

        Console.WriteLine($"Proxy is listening on {tcpListener.LocalEndpoint}");
        started.Set();

        var dumper = NatsProtoDump;
        // var dumper = WsNatsProtoDump;
        
        while (true)
        {
            try
            {
                var clientTcpConnection = tcpListener.AcceptTcpClient();

                var n = Interlocked.Increment(ref proxyServer._client);
                proxyServer.NewClient(n, clientTcpConnection);

                SetupSocket(clientTcpConnection.Client);

                var serverTcpConnection = new TcpClient(serverAddress.ToString(), serverPort);
                SetupSocket(serverTcpConnection.Client);

                Console.WriteLine(
                    $"[{n}] Connected to {clientTcpConnection.Client.LocalEndPoint} -> {serverTcpConnection.Client.RemoteEndPoint}");

#pragma warning disable CS4014
                Task.Run(() =>
                {
                    try
                    {
                        var clientStream = clientTcpConnection.GetStream();
                        var csr = new StreamReader(clientStream, Encoding.Latin1);
                        var csw = new StreamWriter(clientStream, Encoding.Latin1);

                        var serverStream = serverTcpConnection.GetStream();
                        var ssr = new StreamReader(serverStream, Encoding.Latin1);
                        var ssw = new StreamWriter(serverStream, Encoding.Latin1);

                        var state = new Dictionary<string, object>();
                        Task.Run(() =>
                        {
                            try
                            {
                                // Client -> Server
                                while (dumper('C', state, proxyServer, $"[{n}] C-->S", csr, ssw))
                                {
                                }

                                ssr.Close();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Writer task error: {e.Message}");
                            }
                        });

                        // Server -> client
                        while (dumper('S', state, proxyServer, $"[{n}] C<--S", ssr, csw))
                        {
                        }

                        ssr.Close();
                        csr.Close();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Reader task error: {e.Message}");
                    }
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Accept loop error: {e.Message}");
            }
        }
    }

    static bool NatsProtoDump(char orig, Dictionary<string, object> state, ProxyServer proxyServer, string dir, StreamReader sr, StreamWriter sw)
    {
        try
        {
            var line = sr.ReadLine()?.TrimEnd();
            if (line == null) return false;

            if (Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|UNSUB|SUB|RS|\+OK|-ERR)"))
            {
                // TODO: port manipulation
                // if (line.StartsWith("INFO"))
                // {
                //     var json = JsonNode.Parse(line.Substring(5));
                //     
                //     json["port"] = 9999;
                //     var bufferWriter = new SimpleBufferWriter();
                //     var jsonWriter = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { Indented = false });
                //     json.WriteTo(jsonWriter);
                //     jsonWriter.Flush();
                //     var jsonStr = Encoding.UTF8.GetString(bufferWriter.ToArray());
                //     Console.WriteLine($">>>>>>>>>>>INFO JSON: {jsonStr}");
                // }
                proxyServer.WriteCtrl(sw, dir, line);
                return true;
            }


            var match = Regex.Match(line, @"^(?:PUB|HPUB|MSG|HMSG|RMSG).*?(\S+)\s*(\d+)\s*$");
            if (match.Success)
            {
                var token1 = match.Groups[1].Value;
                var size = int.Parse(match.Groups[2].Value);
                var buffer = new char[size + 2];
                var span = buffer.AsSpan();
                while (true)
                {
                    var read = sr.Read(span);
                    if (read == 0) break;
                    if (read == -1) return false;
                    span = span[read..];
                }

                var headerSize = 0;
                if (line.StartsWith("H"))
                {
                    headerSize = int.Parse(token1);
                }

                var index = 0;
                var ascii = new StringBuilder();
                foreach (var c in buffer.AsSpan()[..size])
                {
                    if (index < headerSize)
                    {
                        ascii.Append(c);
                        continue;
                    }
                    switch (c)
                    {
                        case > ' ' and <= '~':
                            ascii.Append(c);
                            break;
                        case ' ':
                            ascii.Append(' ');
                            break;
                        case '\t':
                            ascii.Append("\\t");
                            break;
                        case '\n':
                            ascii.Append("\\n");
                            break;
                        case '\r':
                            ascii.Append("\\r");
                            break;
                        default:
                            ascii.Append('.');
                            // sb.Append(Convert.ToString(c, 16));
                            break;
                    }

                    index++;
                }

                proxyServer.Write(sw, dir, line, buffer, ascii.ToString());

                return true;
            }

            Console.WriteLine($"Error: Unknown protocol: {line}");

            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"{dir} {e.GetType()}: {e.Message}");
            return false;
        }
    }

    class WsState
    {
        public bool Http = true;
    }
    
    static bool WsNatsProtoDump(char orig, Dictionary<string, object> state, ProxyServer proxyServer, string dir, StreamReader sr, StreamWriter sw)
    {
        var stateKey = $"proto-state-{orig}";
        object stateObject;
        lock (state)
        {
            if (!state.TryGetValue(stateKey, out stateObject))
            {
                state[stateKey] = stateObject = new WsState();
            }
        }
        var wsState = (WsState)stateObject;
        
        try
        {
            if (wsState.Http)
            {
                var http = new StringBuilder();
                var httpAscii = new StringBuilder();
                while (true)
                {
                    var line = sr.ReadLine();
                    if (line == null) return false;
                    http.Append(line + "\r\n");
                    httpAscii.Append("  ");
                    httpAscii.AppendLine(line);
                    if (line == string.Empty) break;
                }

                proxyServer.WriteBin(sw, dir, "http", http.ToString(), httpAscii.ToString());
                wsState.Http = false;
                return true;
            }

            static byte[] ReadAsBytes(List<char> buffers, StreamReader reader, int length)
            {
                var charBuffer = new char[length];
                var byteBuffer = new byte[length];
                
                var totalBytesRead = 0;
                while (totalBytesRead < length)
                {
                    var currentBytesRead = reader.Read(charBuffer, totalBytesRead, length - totalBytesRead);
                    if (currentBytesRead == 0) throw new IOException("Unexpected end of stream");
                    totalBytesRead += currentBytesRead;
                }

                // Convert characters to bytes
                for (var i = 0; i < length; i++)
                {
                    byteBuffer[i] = (byte)charBuffer[i];
                }
                
                buffers.AddRange(charBuffer);
                return byteBuffer;
            }
            
            // Websocket frame
            List<char> frame = new();
            var headerBuffer = ReadAsBytes(frame, sr, 2);

            var fin = (headerBuffer[0] & 0b10000000) != 0;
            var opcode = headerBuffer[0] & 0b00001111;
            var isMasked = (headerBuffer[1] & 0b10000000) != 0;
            var payloadLength = headerBuffer[1] & 0b01111111;

            var extendedPayloadLength = Array.Empty<byte>();
            if (payloadLength == 126)
            {
                extendedPayloadLength = ReadAsBytes(frame, sr, 2);
                payloadLength = BitConverter.ToUInt16(extendedPayloadLength.Reverse().ToArray(), 0);
            }
            else if (payloadLength == 127)
            {
                extendedPayloadLength = ReadAsBytes(frame, sr, 8);
                payloadLength = (int)BitConverter.ToUInt64(extendedPayloadLength.Reverse().ToArray(), 0);
            }

            byte[] maskingKey = null;
            if (isMasked)
            {
                maskingKey = ReadAsBytes(frame, sr, 4);
            }

            var payloadData = ReadAsBytes(frame, sr, payloadLength);

            if (isMasked && maskingKey != null)
            {
                for (int i = 0; i < payloadData.Length; i++)
                {
                    payloadData[i] = (byte)(payloadData[i] ^ maskingKey[i % 4]);
                }
            }

            //var payloadText = Encoding.UTF8.GetString(payloadData);
            //Console.WriteLine($"FIN: {fin}, Opcode: {opcode}, Payload: {payloadText}");
            
            var hexDumpString = HexDumpString(payloadData);

            proxyServer.WriteBin(sw, dir, $"FIN: {fin}, Opcode: {opcode}, Masked: {isMasked}", frame.ToArray(), hexDumpString);

            return true;

            Console.WriteLine($"Error: Unknown protocol");

            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"{dir} {e.GetType()}: {e.Message}");
            return false;
        }
    }

    private static string HexDumpString(ReadOnlySpan<byte> span)
    {
        var ascii = new StringBuilder();
        var hexDump = new StringBuilder();
        var indent = true;
        for (int i = 0; i < span.Length; i++)
        {
            if (indent)
            {
                hexDump.Append("  ");
                indent = false;
            }
            
            byte currentByte = (byte)span[i];
            hexDump.AppendFormat("{0:X2} ", currentByte);

            if (currentByte > 31 && currentByte < 127) // Printable ASCII range
            {
                ascii.Append((char)currentByte);
            }
            else
            {
                ascii.Append('.');
            }

            // Add a new line every 16 bytes for formatting
            if ((i + 1) % 16 == 0)
            {
                hexDump.Append(" ");
                hexDump.Append(ascii);
                hexDump.AppendLine();
                indent = true;
                ascii.Clear();
            }
        }

        // Append remaining bytes if total length wasn't a multiple of 16
        if (span.Length % 16 != 0)
        {
            int padding = (16 - (span.Length % 16)) * 3;
            hexDump.Append(' ', padding);
            hexDump.Append(" ");
            hexDump.Append(ascii);
        }

        var hexDumpString = hexDump.ToString();
        return hexDumpString;
    }

    public void Start(string proxyAddress, string serverAddress)
    {
        var proxy = ParseIpv4(proxyAddress);
        var server = ParseIpv4(serverAddress);

        var started1 = new ManualResetEventSlim();
        var started2 = new ManualResetEventSlim();
        Task.Run(() => RunTCPServer(this, started1, proxy.Address, proxy.Port, server.Address, server.Port));
        if (Equals(proxy.Address, IPAddress.Loopback) && Socket.OSSupportsIPv6)
        {
            // this is a hack to get the 'localhost' to work which resolves to IPv6 loopback at least on my machine 
            Task.Run(() => RunTCPServer(this, started2, IPAddress.IPv6Loopback, proxy.Port, server.Address, server.Port));
            started2.Wait();
        }
        started1.Wait();
    }

    private IPEndPoint ParseIpv4(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address is empty");
        }
        
        var parts = address.Split(':');
        if (parts.Length == 1)
        {
            return new IPEndPoint(IPAddress.Loopback, int.Parse(parts[1]));
        }

        if (parts.Length == 2)
        {
            return new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
        }

        throw new ArgumentException("Invalid address format");
    }
}

public class SimpleBufferWriter : IBufferWriter<byte>
{
    private byte[] _buffer;
    private int _position;

    public SimpleBufferWriter(int initialCapacity = 256)
    {
        _buffer = new byte[initialCapacity];
        _position = 0;
    }

    public void Advance(int count)
    {
        if (count < 0 || _position + count > _buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        _position += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_position);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_position);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeHint));
        }

        if (_position + sizeHint > _buffer.Length)
        {
            int newSize = Math.Max(_buffer.Length * 2, _position + sizeHint);
            Array.Resize(ref _buffer, newSize);
        }
    }

    public byte[] ToArray()
    {
        return _buffer.AsSpan(0, _position).ToArray();
    }
}
