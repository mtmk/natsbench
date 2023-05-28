using System.Text.RegularExpressions;

namespace client1;

public class Program
{
    static void Main()
    {
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
            else
            {
                Println("Unknown command");
            }
        }
        
        natsClient.Close();
    }
}