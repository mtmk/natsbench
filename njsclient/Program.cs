using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("");
        Console.WriteLine("NATS JS client playground");
        Console.WriteLine("");

        var nats = new NatsConnection();

        void Print(string message) => Console.Write(message);

        void Println(string message) => Console.WriteLine(message);
        
        bool prompt = true;

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
                Println("NATS .NET v2 JetStream client playground");
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
                Println("stream create <stream> <subjects>");
                Println("stream delete <name>");
                Println("");
                Println("consumer create <stream> <consumer>");
                Println("");
                Println("jspub <subject> <payload>");
                Println("consume <stream> <consumer> [noack]");
                Println("");
                Println("help                                           this message");
                Println("");
            }
            else if (cmd.StartsWith("ping"))
            {
                var time = await nats.PingAsync();
                Println($"Ping {time}");
            }
            else if (cmd.StartsWith("pub"))
            {
                var match = Regex.Match(cmd, @"^pub\s+(\S+)\s+(?:(\S+)\s+)?(\S+)$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var replyTo = match.Groups[2].Value;
                var payload = match.Groups[3].Value;
                Println($"Publish to {subject}");
                // natsClient.Pub(subject, replyTo, payload);
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
                // natsClient.HPub(subject, replyTo, payload, headers);
            }
            else if (cmd.StartsWith("sub"))
            {
                var match = Regex.Match(cmd, @"^sub\s+(\S+)(?:\s+(\S+))?$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var queueGroup = match.Groups[2].Value;
                Println($"Subscribe to {subject}");
                // var sid = natsClient.Sub(subject, queueGroup, m =>
                // {
                //     if (display)
                //     {
                //         Println($"Message received ({queueGroup}): {m}");
                //     }
                //
                //     if (!string.IsNullOrEmpty(m.ReplyTo))
                //     {
                //         natsClient.Pub(m.ReplyTo, "", $"Got your message '{m.Payload}'");
                //     }
                // });
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
                // natsClient.UnSub(sid, max);
            }
            
            // JetStream Stuff
            else if (cmd.StartsWith("stream create"))
            {
                var match = Regex.Match(cmd, @"^stream create (\w+)\s+([\w,\.\*>]+)\s*$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                var subjects = match.Groups[2].Value;
                Println($"Creating stream {stream}...");
                var js = new JSContext(nats, new JSOptions());
                var r = await js.CreateStreamAsync(new StreamConfiguration
                {
                    Name = stream,
                    Subjects = subjects.Split(','),
                });
                Println($"{r.Error}{r.Response}");
            }
            else if (cmd.StartsWith("stream delete"))
            {
                var match = Regex.Match(cmd, @"^stream delete (\w+)\s*$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                var js = new JSContext(nats, new JSOptions());
                var r = await js.DeleteStreamAsync(stream);
                Println($"{r.Error}{r.Response}");
            }
            else if (cmd.StartsWith("consumer create"))
            {
                var match = Regex.Match(cmd, @"^consumer create (\w+)\s+(\w+)\s*$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                var consumer = match.Groups[2].Value;
                var js = new JSContext(nats, new JSOptions());
                var r = await js.CreateConsumerAsync(new ConsumerCreateRequest
                {
                    StreamName = stream,
                    Config = new ConsumerConfiguration
                    {
                        Name = consumer,
                        DurableName = consumer,

                        // Turn on ACK so we can test them below
                        AckPolicy = ConsumerConfigurationAckPolicy.@explicit,

                        // Effectively set message expiry for the consumer
                        // so that unacknowledged messages can be put back into
                        // the consumer to be delivered again (in a sense).
                        // This is to make below consumer tests work.
                        AckWait = 2_000_000_000, // 2 seconds
                    },
                });
                Println($"{r.Error}{r.Response}");
            }
            else if (cmd.StartsWith("jspub"))
            {
                var match = Regex.Match(cmd, @"^jspub (\S+)\s+(\S+)\s*$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var payload = match.Groups[2].Value;
                var js = new JSContext(nats, new JSOptions());
                var r = await js.PublishAsync(subject, new Payload { Message = payload });
                Println($"[PUB] {r}");
            }
            else if (cmd.StartsWith("consume"))
            {
                var match = Regex.Match(cmd, @"^consume (\S+)\s+(\S+)(?:\s+(\S+))?\s*$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                var consumer = match.Groups[2].Value;
                var mod = match.Groups[3].Value;
                Task.Run(async () =>
                {
                    var js = new JSContext(nats, new JSOptions());
                    await foreach (var msg in js.ConsumeAsync(
                                       stream: stream,
                                       consumer: consumer,
                                       request: new ConsumerGetnextRequest
                                       {
                                           Batch = 10,
                                           Expires = 0,
                                       },
                                       requestOpts: new NatsSubOpts { CanBeCancelled = true },
                                       cancellationToken: CancellationToken.None))
                    {
                        //Println($"{msg}");
                        Println($"[CON][{consumer}] {msg.Subject}: {Encoding.UTF8.GetString(msg.Data.Span)}");

                        if (mod != "noack")
                        {
                            await msg.ReplyAsync(new ReadOnlySequence<byte>("+ACK"u8.ToArray()),
                                cancellationToken: CancellationToken.None);
                            Println($"[CON][{consumer}] +ACK {msg.ReplyTo}");
                        }
                        //break;
                    }
                });
            }
            
            else
            {
                Println("Unknown command");
            }
        }
    }
}

internal class Payload
{
    public string Message { get; set; }
}