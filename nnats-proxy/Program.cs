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
        var proxyServerPort = 4222;
        var natsServerPort = 4333;
        
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
        server.Start(proxyServerPort, natsServerPort);


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

}