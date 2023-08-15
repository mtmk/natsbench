using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public class Program
{
    static void Main()
    {
        var port1 = 4222;
        var port2 = 4333;
        var help = (Func<Server, string>)(
            s => $$"""

                   NATS Wire Protocol Analysing TCP Proxy
                   
                     h, ?, help         This message
                     drop <client-id>   Close TCP connection of client
                     ctrl               Toggle displaying core control messages
                     js                 Toggle JetStream summarization
                     js-hb              Toggle suppressing JetStream Heartbeat messages
                     js-msg             Toggle displaying JetStream messages
                     q, quit            Quit program and stop nats-server

                   Display core control messages : {{s.DisplayCtrl}}
                    Suppress JetStream Heartbeat : {{s.SuppressHeartbeats}}
                    Summarize JetStream messages : {{s.JetStreamSummarization}}
                      Display JetStream messages : {{s.DisplayMessages}}
                                  
                   """);

        Console.WriteLine($"NATS Simple Client Protocol Proxy");
        Console.WriteLine($"Starting nats-server");
        var started = new ManualResetEventSlim();
        var natsServer = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "nats-server",
                Arguments = $"-p {port2} -js",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }
        };

        void DataReceived(object _, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            Console.WriteLine(e.Data);
            if (e.Data.Contains("Server is ready"))
                started.Set();
        }

        natsServer.OutputDataReceived += DataReceived;
        natsServer.ErrorDataReceived += DataReceived;
        natsServer.Start();
        natsServer.BeginErrorReadLine();
        natsServer.BeginOutputReadLine();
        ChildProcessTracker.AddProcess(natsServer);

        if (!started.Wait(5000))
        {
            Console.WriteLine("Error: Can't see nats-server started");
            return;
        }

        Console.WriteLine("Started nats-server");

        var server = new Server();

        var started1 = new ManualResetEventSlim();
        var started2 = new ManualResetEventSlim();
        Task.Run(() => RunServer(server, started1, IPAddress.Loopback, port1, port2));
        if (Socket.OSSupportsIPv6)
        {
            Task.Run(() => RunServer(server, started2, IPAddress.IPv6Loopback, port1, port2));
            started2.Wait();
        }
        started1.Wait();

        Console.WriteLine();
        Console.WriteLine(help(server));
        
        var prt = Stopwatch.StartNew();
        var prompt = true;
        while (true)
        {
            if (prompt)
            {
                Console.Write("nnats-proxy> ");
            }
            
            var cmd = Console.ReadLine();
            if (Regex.IsMatch(cmd, @"^\s*$"))
            {
                prompt = prt.Elapsed > TimeSpan.FromSeconds(.7);
                prt.Restart();
                continue;
            }

            prompt = true;
            
            if (Regex.IsMatch(cmd, @"^\s*(\?|h|help)\s*$"))
            {
                Console.WriteLine(help(server));
            }
            else if (Regex.IsMatch(cmd, @"^\s*(q|quit)\s*$"))
            {
                Console.WriteLine("Bye");
                break;
            }
            else if (Regex.IsMatch(cmd, @"^\s*(ctrl)\s*$"))
            {
                server.DisplayCtrl = !server.DisplayCtrl;
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js)\s*$"))
            {
                server.JetStreamSummarization = !server.JetStreamSummarization;
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js-hb)\s*$"))
            {
                server.SuppressHeartbeats = !server.SuppressHeartbeats;
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js-msg)\s*$"))
            {
                server.DisplayMessages = !server.DisplayMessages;
            }
            else if (cmd.StartsWith("drop"))
            {
                var match = Regex.Match(cmd, @"^\s*drop\s+(\d+)\s*$");
                if (match.Success)
                {
                    var id = int.Parse(match.Groups[1].Value);
                    server.Drop(id);
                }
                else
                {
                    Console.WriteLine($"Error: Can't parse drop command: {cmd}");
                }
            }
            else
            {
                Console.WriteLine($"Error: Can't parse command: {cmd}");
            }
        }
    }

    static void SetupSocket(Socket socket)
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

        socket.NoDelay = true;
    }

    private static int _client = 0;

    class Server
    {
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new();

        private int _js;
        public bool JetStreamSummarization
        {
            get => Volatile.Read(ref _js) == 1;
            set => Interlocked.Exchange(ref _js, value ? 1 : 0);
        }

        private int _shb;
        public bool SuppressHeartbeats
        {
            get => Volatile.Read(ref _shb) == 1;
            set => Interlocked.Exchange(ref _shb, value ? 1 : 0);
        }

        private int _msg = 1;
        public bool DisplayMessages
        {
            get => Volatile.Read(ref _msg) == 1;
            set => Interlocked.Exchange(ref _msg, value ? 1 : 0);
        }

        private int _ctrl;
        public bool DisplayCtrl
        {
            get => Volatile.Read(ref _ctrl) == 1;
            set => Interlocked.Exchange(ref _ctrl, value ? 1 : 0);
        }

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
                        // NATS/1.0 100 Idle Heartbeat\r\nNats-Last-Consumer: 107\r\nNats-Last-Stream: 107\r\n\r\n
                        var lastConsumer = m1.Groups[1].Value;
                        var lastStream = m1.Groups[2].Value;
                        js = $"[C] [IHB] consumer:{lastConsumer} stream:{lastStream}";
                    }
                    //else if ((m1 = Regex.Match(buffer, @"")).Success)
                    else if ((m1 = Regex.Match(ascii, @"408 Request Timeout\\r\\nNats-Pending-Messages: (\d+)\\r\\nNats-Pending-Bytes: (\d+)")).Success)
                    {
                        // NATS/1.0 408 Request Timeout\r\nNats-Pending-Messages: 10\r\nNats-Pending-Bytes: 0\r\n\r\n
                        var pendingMsgs = m1.Groups[1].Value;
                        var pendingBytes = m1.Groups[2].Value;
                        js = $"[C] [RTO] pending msgs:{pendingMsgs} bytes:{pendingBytes}";
                    }
                    else if ((m1 = Regex.Match(line, @"^PUB \$JS\.API\.CONSUMER\.MSG\.NEXT\.(\w+)\.(\w+)")).Success)
                    {
                        // PUB $JS.API.CONSUMER.MSG.NEXT.s1.c2 _INBOX.82YIGQ8W3TI0XNGETMHF03 77
                        // {"batch":10,"max_bytes":0,"idle_heartbeat":15000000000,"expires":30000000000}
                        var stream = m1.Groups[1].Value;
                        var consumer = m1.Groups[2].Value;
                        var json = JsonNode.Parse(ascii);
                        var batch = json["batch"]?.GetValue<int>();
                        js = $"[C] [NXT] {stream}/{consumer} batch:{batch}";
                    }
                    else if (
                        (m1 = Regex.Match(line, @"^PUB \$JS\.ACK\.(\w+)\.(\w+)\.(\d+)\.(\d+)\.(\d+)\.(\d+)\.(\d+)")).Success
                        && (m2 = Regex.Match(ascii, @"^\+ACK$")).Success)
                    {
                        if (DisplayMessages)
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
                            js = $"[M] [ACK] {stream}/{consumer} v1:{v1} v2:{v2} v3:{v3} v4:{v4} v5:{v5}";
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
                            js = $"[M] [MSG] {subject} {stream}/{consumer} v1:{v1} v2:{v2} v3:{v3} v4:{v4} v5:{v5}"
                                // + $"\n    {new string(' ', dir.Length)} {ascii}"
                                ;
                        }
                        else
                        {
                            js = "";
                            skip = true;
                        }
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
    
    static void RunServer(Server server, ManualResetEventSlim started, IPAddress address, int proxyPort, int serverPort)
    {
        var tcpListener = new TcpListener(address, proxyPort);

        SetupSocket(tcpListener.Server);

        tcpListener.Start();

        Console.WriteLine($"Proxy is listening on {tcpListener.LocalEndpoint}");
        started.Set();
        
        while (true)
        {
            var clientTcpConnection = tcpListener.AcceptTcpClient();

            var n = Interlocked.Increment(ref _client);
            server.NewClient(n, clientTcpConnection);
            
            SetupSocket(clientTcpConnection.Client);

            var serverTcpConnection = new TcpClient("127.0.0.1", serverPort);
            SetupSocket(serverTcpConnection.Client);

            Console.WriteLine($"[{n}] Connected to {clientTcpConnection.Client.LocalEndPoint} -> {serverTcpConnection.Client.RemoteEndPoint}");

#pragma warning disable CS4014
            Task.Run(() =>
            {
                var clientStream = clientTcpConnection.GetStream();
                var csr = new StreamReader(clientStream, Encoding.ASCII);
                var csw = new StreamWriter(clientStream, Encoding.ASCII);

                var serverStream = serverTcpConnection.GetStream();
                var ssr = new StreamReader(serverStream, Encoding.ASCII);
                var ssw = new StreamWriter(serverStream, Encoding.ASCII);

                Task.Run(() =>
                {
                    // Client -> Server
                    while (NatsProtoDump(server, $"[{n}] -->", csr, ssw))
                    {
                    }
                });
                
                // Server -> client
                while (NatsProtoDump(server, $"[{n}] <--", ssr, csw))
                {
                }
            });
        }
    }

    static bool NatsProtoDump(Server server, string dir, StreamReader sr, StreamWriter sw)
    {
        var line = sr.ReadLine();
        if (line == null) return false;

        if (Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|UNSUB|SUB|\+OK|-ERR)"))
        {
            server.WriteCtrl(sw, dir, line);
            return true;
        }


        var match = Regex.Match(line, @"^(?:PUB|HPUB|MSG|HMSG).*?(\d+)\s*$");
        if (match.Success)
        {
            var size = int.Parse(match.Groups[1].Value);
            var buffer = new char[size + 2];
            var span = buffer.AsSpan();
            while (true)
            {
                var read = sr.Read(span);
                if (read == 0) break;
                if (read == -1) return false;
                span = span[read..];
            }

            var ascii = new StringBuilder();
            foreach (var c in buffer.AsSpan()[..size])
            {
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
            }

            server.Write(sw, dir, line, buffer, ascii.ToString());
            
            return true;
        }

        Console.WriteLine($"Error: Unknown protocol: {line}");

        return false;
    }
}