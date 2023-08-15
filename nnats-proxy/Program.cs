using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

public class Program
{
    static void Main()
    {
        var port1 = 4222;
        var port2 = 4333;

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

        Task.Run(() => RunServer(server, IPAddress.Loopback, port1, port2));
        if (Socket.OSSupportsIPv6)
        {
            Task.Run(() => RunServer(server, IPAddress.IPv6Loopback, port1, port2));
        }

        while (true)
        {
            Console.Write("nnats-proxy> ");
            var cmd = Console.ReadLine();
            if (Regex.IsMatch(cmd, @"^\s*$"))
            {
            }
            else if (Regex.IsMatch(cmd, @"^\s*(\?|h|help)\s*$"))
            {
                Console.WriteLine("""
                                  
                                  NATS Wire Protocol Analysing TCP Proxy
                                  
                                    h, ?, help         This message
                                    drop <client-id>   Close TCP connection of client
                                    q, quit            Quit program and stop nats-server
                                  
                                  """);
            }
            else if (Regex.IsMatch(cmd, @"^\s*(q|quit)\s*$"))
            {
                Console.WriteLine("Bye");
                break;
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
    }
    
    static void RunServer(Server server, IPAddress address, int proxyPort, int serverPort)
    {
        var tcpListener = new TcpListener(address, proxyPort);

        SetupSocket(tcpListener.Server);

        tcpListener.Start();

        Console.WriteLine($"Proxy is listening on {tcpListener.LocalEndpoint}");
        
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
                    while (NatsProtoDump($"\n[{n}] -->", csr, ssw))
                    {
                    }
                });
                
                // Server -> client
                while (NatsProtoDump($"\n[{n}] <--", ssr, csw))
                {
                }
            });
        }
    }

    static bool NatsProtoDump(string dir, StreamReader sr, StreamWriter sw)
    {
        var line = sr.ReadLine();
        if (line == null) return false;

        if (Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|UNSUB|SUB|\+OK|-ERR)"))
        {
            // Ignore control messages
            // if (!Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|\+OK)"))
            Console.WriteLine($"{dir} {line}");

            sw.WriteLine(line);
            sw.Flush();
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

            var sb = new StringBuilder();
            foreach (var c in buffer.AsSpan()[..size])
            {
                switch (c)
                {
                    case > ' ' and <= '~':
                        sb.Append(c);
                        break;
                    case ' ':
                        sb.Append(' ');
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        sb.Append('.');
                        // sb.Append(Convert.ToString(c, 16));
                        break;
                }
            }

            sw.WriteLine(line);
            sw.Write(buffer);
            sw.Flush();
            Console.WriteLine($"{dir} {line}\n{new string(' ', dir.Length)} {sb}");
            return true;
        }

        Console.WriteLine($"Error: Unknown protocol: {line}");

        return false;
    }
}