using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace nnats_proxy;

public class Program
{
    static void Main()
    {
        var port1 = 4222;
        var port2 = 4333;
        var help = (Func<ProxyServer, string>)(
            s => $$"""

                   NATS Wire Protocol Analysing TCP Proxy
                   
                     h, ?, help         This message
                     drop <client-id>   Close TCP connection of client
                     ctrl               Toggle displaying core control messages
                     js                 Toggle JetStream summarization
                     js-hb              Toggle displaying JetStream Heartbeat messages
                     js-shb             Toggle suppressing JetStream Heartbeat messages
                     js-msg             Toggle displaying JetStream messages
                     q, quit            Quit program and stop nats-server

                   Display core control messages : {{s.DisplayCtrl}}
                    Suppress JetStream Heartbeat : {{s.SuppressHeartbeats}}
                    Summarize JetStream messages : {{s.JetStreamSummarization}}
                      Display JetStream messages : {{s.DisplayMessages}}
                                  
                   """);

        Console.WriteLine($"NATS Simple Client Protocol Proxy");

        // Start or use external!
        // using var natsServer = new NatsServer().Start(port2);

        Console.WriteLine("Started nats-server");

        var server = new ProxyServer();

        var started1 = new ManualResetEventSlim();
        var started2 = new ManualResetEventSlim();
        Task.Run(() => RunProxyServer(server, started1, IPAddress.Loopback, port1, port2));
        if (Socket.OSSupportsIPv6)
        {
            Task.Run(() => RunProxyServer(server, started2, IPAddress.IPv6Loopback, port1, port2));
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
            
            // Reset prompt if less than 700ms so we can
            // create some space.
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
                server.DisplayCtrl.Toggle();
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js)\s*$"))
            {
                server.JetStreamSummarization.Toggle();
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js-shb)\s*$"))
            {
                server.SuppressHeartbeats.Toggle();
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js-hb)\s*$"))
            {
                server.DisplayHeartbeats.Toggle();
            }
            else if (Regex.IsMatch(cmd, @"^\s*(js-msg)\s*$"))
            {
                server.DisplayMessages.Toggle();
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

                prompt = false;
            }
            else
            {
                Console.WriteLine($"Error: Can't parse command: {cmd}");
            }
        }
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

    private static int _client = 0;
    
    static void RunProxyServer(ProxyServer proxyServer, ManualResetEventSlim started, IPAddress address, int proxyPort, int serverPort)
    {
        var tcpListener = new TcpListener(address, proxyPort);

        SetupSocket(tcpListener.Server);

        tcpListener.Start();

        Console.WriteLine($"Proxy is listening on {tcpListener.LocalEndpoint}");
        started.Set();
        
        while (true)
        {
            try
            {
                var clientTcpConnection = tcpListener.AcceptTcpClient();

                var n = Interlocked.Increment(ref _client);
                proxyServer.NewClient(n, clientTcpConnection);

                SetupSocket(clientTcpConnection.Client);

                var serverTcpConnection = new TcpClient("127.0.0.1", serverPort);
                SetupSocket(serverTcpConnection.Client);

                Console.WriteLine(
                    $"[{n}] Connected to {clientTcpConnection.Client.LocalEndPoint} -> {serverTcpConnection.Client.RemoteEndPoint}");

#pragma warning disable CS4014
                Task.Run(() =>
                {
                    try
                    {
                        var clientStream = clientTcpConnection.GetStream();
                        var csr = new StreamReader(clientStream, Encoding.ASCII);
                        var csw = new StreamWriter(clientStream, Encoding.ASCII);

                        var serverStream = serverTcpConnection.GetStream();
                        var ssr = new StreamReader(serverStream, Encoding.ASCII);
                        var ssw = new StreamWriter(serverStream, Encoding.ASCII);

                        Task.Run(() =>
                        {
                            try
                            {
                                // Client -> Server
                                while (NatsProtoDump(proxyServer, $"[{n}] -->", csr, ssw))
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
                        while (NatsProtoDump(proxyServer, $"[{n}] <--", ssr, csw))
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

    static bool NatsProtoDump(ProxyServer proxyServer, string dir, StreamReader sr, StreamWriter sw)
    {
        try
        {
            var line = sr.ReadLine();
            if (line == null) return false;

            if (Regex.IsMatch(line, @"^(INFO|CONNECT|PING|PONG|UNSUB|SUB|\+OK|-ERR)"))
            {
                proxyServer.WriteCtrl(sw, dir, line);
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
}