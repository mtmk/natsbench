using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace client1;

public class Program
{
    private static int consumerInboxIndex;
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        if (args.Length > 0 && args[0] == "bench1")
        {
            Bench1(args.Skip(1).ToArray());
            return;
        }
        if (args.Length > 0 && args[0] == "bench2")
        {
            Bench2(args.Skip(1).ToArray());
            return;
        }
        
        Console.WriteLine("");
        Console.WriteLine("NATS client playground");
        Console.WriteLine("");

        var natsClient = NatsClient.Connect();

        void Print(string message) => natsClient?.Log(message);
        
        void Println(string message) => natsClient?.LogLine(message);
        
        bool prompt = false;
        bool display = true;
        while (true)
        {
            if (prompt)
            {
                Print("> ");
            }

            prompt = false;
            
            var cmd = Console.ReadLine();
            
            if (cmd == null || cmd.StartsWith("q")) break;

            if (Regex.IsMatch(cmd, @"^\s*$"))
            {
                prompt = true;
                continue;
            }

            if (Regex.IsMatch(cmd, @"^\s*(?:\?|h|help)\s*$"))
            {
                Println("");
                Println("NATS client playground");
                Println("");
                Println("sub  <subject> [queue-group]                   subscribe");
                Println("     e.g. sub foo bar");
                Println("          sub foo");
                Println("");
                Println("unsub <sid> [max-msg]                          unsubscribe");
                Println("     e.g. unsub 1");
                Println("          unsub 1 10");
                Println("");
                Println("pub  <subject> [reply-to] <payload>            publish");
                Println("     e.g. pub foo bar my_msg");
                Println("          pub foo my_msg");
                Println("");
                Println("hpub <subject> [reply-to] <payload> <headers>  publish with headers");
                Println("     e.g. hpub foo bar my_msg A:1,B:2");
                Println("          hpub foo my_msg A:1,B:2");
                Println("");
                Println("ctrl <on|off>                                  control message log");
                Println("");
                Println("display <on|off>                               application message log");
                Println("");
                Println("help                                           this message");
                Println("");
                Println($"ctrl:{natsClient.Ctrl} display:{display}");
            }
            else if (Regex.IsMatch(cmd, @"^\s*ctrl\s+on\s*$"))
            {
                natsClient.CtrlOn();
            }
            else if (Regex.IsMatch(cmd, @"^\s*ctrl\s+off\s*$"))
            {
                natsClient.CtrlOff();
            }
            else if (Regex.IsMatch(cmd, @"^\s*display\s+on\s*$"))
            {
                display = true;
            }
            else if (Regex.IsMatch(cmd, @"^\s*display\s+off\s*$"))
            {
                display = false;
            }
            else if (cmd.StartsWith("ping"))
            {
                natsClient.Ping();
            }
            else if (cmd.StartsWith("pub"))
            {
                var match = Regex.Match(cmd, @"^pub\s+(\S+)\s+(?:(\S+)\s+)?(\S+)$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var replyTo = match.Groups[2].Value;
                var payload = match.Groups[3].Value;
                Println($"Publish to {subject}");
                natsClient.Pub(subject, replyTo, payload);
            }
            else if (cmd.StartsWith("hpub"))
            {
                var match = Regex.Match(cmd, @"^hpub\s+(\S+)\s+(?:(\S+)\s+)?(\S+)\s+(\S+)$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var replyTo = match.Groups[2].Value;
                var payload = match.Groups[3].Value;
                var headers = match.Groups[4].Value;
                Println($"Publish to {subject}");
                natsClient.HPub(subject, replyTo, payload, headers);
            }
            else if (cmd.StartsWith("sub"))
            {
                var match = Regex.Match(cmd, @"^sub\s+(\S+)(?:\s+(\S+))?$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var queueGroup = match.Groups[2].Value;
                Println($"Subscribe to {subject}");
                var sid = natsClient.Sub(subject, queueGroup, m =>
                {
                    if (display)
                    {
                        Println($"Message received ({queueGroup}): {m}");
                    }
                    
                    if (!string.IsNullOrEmpty(m.ReplyTo))
                    {
                        natsClient.Pub(m.ReplyTo, "", $"Got your message '{m.Payload}'");
                    }
                });
            }
            else if (cmd.StartsWith("unsub"))
            {
                // https://docs.nats.io/reference/reference-protocols/nats-protocol#unsub
                // UNSUB <sid> [max_msgs]\r\n
                var match = Regex.Match(cmd, @"^unsub\s+(\S+)(?:\s+(\d+))?\s*$");
                if (!match.Success) continue;
                var sid = int.Parse(match.Groups[1].Value);
                var value = match.Groups[2].Value;
                int? max = string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);
                Println($"Unsubscribe from {sid}");
                natsClient.UnSub(sid, max);
            }
            else if (cmd.StartsWith("con"))
            {
                var match = Regex.Match(cmd, @"^con\s+(\S+)(?:\s+(\S+))?$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                var consumer = match.Groups[2].Value;
                Println($"Consuming from {stream}:{consumer}");

                int sid = -1;
                try
                {
                    var inbox = $"_INBOX.{Interlocked.Increment(ref consumerInboxIndex)}";
                    var doAck = (AtomicBool)true;
                    sid = natsClient.Sub(inbox, "", m =>
                    {
                        if (display)
                        {
                            if (m.Subject == inbox)
                            {
                                Println(m.DumpHeadersParsed());
                            }
                            else
                            {
                                Println($"📨 {m.Payload}");
                            }
                        }

                        if (doAck && !string.IsNullOrEmpty(m.ReplyTo))
                        {
                            natsClient.Pub(m.ReplyTo, "", "+ACK");
                        }
                    });

                    var help = $$"""
                                    Consumer Mode {{ stream }}.{{ consumer }}

                                    n<batch> <bytes> <expire> <hb>  Pull next batch
                                    ack                             Toggle ack
                                    q                               quit

                                    ack: {{ doAck }}
                                    
                                    """;
                    Println(message: help);
                    while (true)
                    {
                        Print($"{stream}.{consumer}> ");
                        var line = (Console.ReadLine() ?? throw null).Trim();

                        if (line == "q") break;
                        if (line == "") continue;

                        Match m;
                        if ((m = Regex.Match(line, @"^\s*n\s*(\d+)(?:\s+(\d+)\s+(\d+)\s+(\d+))?\s*$")).Success)
                        {
                            // PUB $JS.API.CONSUMER.MSG.NEXT.s1.c2 _INBOX.143fbf756e154686967be929fa26cc55 63
                            // {"expires":30000000000,"batch":10,"idle_heartbeat":15000000000}
                            var batch = int.Parse(m.Groups[1].Value);
                            var bytes = int.Parse(m.Groups[2].Value);
                            var expire = int.Parse(m.Groups[3].Value);
                            var hb = int.Parse(m.Groups[4].Value);
                            var api = $"$JS.API.CONSUMER.MSG.NEXT.{stream}.{consumer}";
                            var payload =
                                batch <= 1
                                    ? ""
                                    : $$"""
                                        {"batch":{{batch}},
                                         "max_bytes":{{bytes}},
                                         "expires":{{expire}}000000000,
                                         "idle_heartbeat":{{hb}}000000000}
                                        """;
                            
                            natsClient.Pub(subject: api, replyTo: inbox, payload: payload);
                        }
                        else if (line == "ack")
                        {
                            doAck.Toggle();
                        }
                        else if (line == "h")
                        {
                            Println(message: help);
                        }
                        else
                        {
                            Println("Unknown consumer command.");
                        }
                    }
                }
                finally
                {
                    if (sid > 0)
                        natsClient.UnSub(sid, default);
                }
            }
            else
            {
                Println("Unknown command");
            }
        }
        
        natsClient.Close();
    }
    
    private static void Bench1(string[] args)
    {
        Console.WriteLine("Starting bench...");
        
        var nats = NatsClient.Connect();
        var signal = new ManualResetEventSlim();

        var count = 0;
        var stopwatch = Stopwatch.StartNew();
        
        nats.Sub("foo", "", m =>
        {
            Interlocked.Increment(ref count);
            //Console.WriteLine($"[RCV] {m}");
            signal.Set();
        });

        var message = "my_message";
        for (int i = 0; i < 100_000; i++)
        {
            nats.Pub("foo", "", message);
            signal.Wait();
            signal.Reset();
        }
        
        Console.WriteLine($"count:{Volatile.Read(ref count)}");
        Console.WriteLine($"{stopwatch.Elapsed}");
    }

    private static void Bench2(string[] args)
    {
        var report = BenchmarkRunner.Run<NatsBench>();
        foreach (var r in report.Reports)
        {
            Console.WriteLine($"Report");
            Console.WriteLine($"BytesAllocatedPerOperation:{r.GcStats.GetBytesAllocatedPerOperation(r.BenchmarkCase)}");
            Console.WriteLine($"GEN0:{r.GcStats.Gen0Collections}");
            Console.WriteLine($"GEN1:{r.GcStats.Gen1Collections}");
            Console.WriteLine($"GEN2:{r.GcStats.Gen2Collections}");
        }
    }
}