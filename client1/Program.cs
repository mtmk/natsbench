using System.Text.RegularExpressions;

namespace client1;

public class Program
{
    static void Main()
    {
        var natsClient = NatsClient.Connect();
        
        while (true)
        {
            Console.Write("> ");
            var cmd = Console.ReadLine();
            
            if (cmd == null || cmd.StartsWith("q")) break;
            
            if (Regex.IsMatch(cmd, @"^\s*$")) continue;

            if (Regex.IsMatch(cmd, @"^\s*(?:\?|h|help)\s*$"))
            {
                Console.WriteLine("sub  <subject> [queue-group]                   subscribe");
                Console.WriteLine("     e.g. sub foo bar");
                Console.WriteLine("          sub foo");
                Console.WriteLine("unsub <sid> [max-msg]                          unsubscribe");
                Console.WriteLine("     e.g. unsub 1");
                Console.WriteLine("          unsub 1 10");
                Console.WriteLine("pub  <subject> [reply-to] <payload>            publish");
                Console.WriteLine("     e.g. pub foo bar my_msg");
                Console.WriteLine("          pub foo my_msg");
                Console.WriteLine("hpub <subject> [reply-to] <payload> <headers>  publish with headers");
                Console.WriteLine("     e.g. hpub foo bar my_msg A:1,B:2");
                Console.WriteLine("          hpub foo my_msg A:1,B:2");
                Console.WriteLine("ctrl <on|off>                                  control message log");
                Console.WriteLine("help                                           this message");
            }
            else if (Regex.IsMatch(cmd, @"^\s*ctrl\s+on\s*$"))
            {
                natsClient.CtrlOn();
            }
            else if (Regex.IsMatch(cmd, @"^\s*ctrl\s+off\s*$"))
            {
                natsClient.CtrlOff();
            }
            else if (cmd.StartsWith("pub"))
            {
                var match = Regex.Match(cmd, @"^pub\s+(\S+)\s+(?:(\S+)\s+)?(\S+)$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var replyTo = match.Groups[2].Value;
                var payload = match.Groups[3].Value;
                Console.WriteLine($"Publish to {subject}");
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
                Console.WriteLine($"Publish to {subject}");
                natsClient.HPub(subject, replyTo, payload, headers);
            }
            else if (cmd.StartsWith("sub"))
            {
                var match = Regex.Match(cmd, @"^sub\s+(\S+)(?:\s+(\S+))?$");
                if (!match.Success) continue;
                var subject = match.Groups[1].Value;
                var queueGroup = match.Groups[2].Value;
                Console.WriteLine($"Subscribe to {subject}");
                natsClient.Sub(subject, queueGroup, m =>
                {
                    Console.WriteLine($"Message received ({queueGroup}): {m}");
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
                var sid = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                int? max = string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);
                Console.WriteLine($"Unsubscribe from {sid}");
                natsClient.UnSub(sid, max);
            }
            else
            {
                Console.WriteLine("Unknown command");
            }
        }
        
        natsClient.Close();
    }
}