// See https://aka.ms/new-console-template for more information

using System.Dynamic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using client1;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

public class Program
{
    static void Main(string[] args)
    {
        // AnsiConsole.MarkupLine("2[underline red]Hello[/] World!");
        //
        // var ys = new YamlStream();
        // ys.Load(new StreamReader("config.yml"));
        // var config = ys.Documents[0].RootNode;//.ToDynamic();
        //
        // var sectionTitle = AnsiConsole.Prompt(
        //     new SelectionPrompt<string>()
        //         .Title("Section?")
        //         .AddChoices(config["sections"].ToList().Select(y => y["title"].ToString())));
        //
        // AnsiConsole.MarkupLine($"Section: {sectionTitle}");
        //
        //
        // YamlNode section = config["sections"].ToList().First(y => y["title"].ToString() == sectionTitle);
        //
        // var subjectx = AnsiConsole.Prompt(
        //     new SelectionPrompt<string>()
        //         .Title("Endpoint?")
        //         .AddChoices(section["endpoints"].ToList().Select(y => y["subject"].ToString())));
        //
        // AnsiConsole.MarkupLine($"Endpoint: {subjectx}");
        //
        //
        // AnsiConsole.MarkupLine($"bye!");
        //
        // return;
        //
        Console.WriteLine("");
        Console.WriteLine("NATS JS client playground");
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
                Println("NATS JetStream client playground");
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
                Println("ctrl <on|off>                                  control message log");
                Println("");
                Println("display <on|off>                               application message log");
                Println("");
                Println("dump <on|off>                                  message log");
                Println("");
                Println("help                                           this message");
                Println("");
                Println($"ctrl:{natsClient.Ctrl} dump:{natsClient.Dump} display:{display}");
            }
            else if (Regex.IsMatch(cmd, @"^\s*ctrl\s+on\s*$"))
            {
                natsClient.CtrlOn();
            }
            else if (Regex.IsMatch(cmd, @"^\s*ctrl\s+off\s*$"))
            {
                natsClient.CtrlOff();
            }
            else if (Regex.IsMatch(cmd, @"^\s*dump\s+on\s*$"))
            {
                natsClient.DumpOn();
            }
            else if (Regex.IsMatch(cmd, @"^\s*dump\s+off\s*$"))
            {
                natsClient.DumpOff();
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
            
            // JetStream Stuff
            else if (cmd.StartsWith("stream create"))
            {
                var match = Regex.Match(cmd, @"^stream create (\w+)\s+(\w+)\s*$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                var subject = match.Groups[2].Value;
                Println($"Creating stream {stream}...");
                var response = natsClient.Req($"$JS.API.STREAM.CREATE.{stream}", $$"""
                {"name":"{{stream}}",
                 "subjects":["{{subject}}"],
                 "retention":"limits",
                 "storage":"file",
                 "discard":"old"}
                """);
                Println($"Response: {response}");
            }
            else if (cmd.StartsWith("stream delete"))
            {
                var match = Regex.Match(cmd, @"^stream delete (\w+)\s*$");
                if (!match.Success) continue;
                var stream = match.Groups[1].Value;
                Println($"Deleting stream {stream}...");
                var response = natsClient.Req($"$JS.API.STREAM.DELETE.{stream}", "");
                Println($"Response: {response}");
            }
            
            else
            {
                Println("Unknown command");
            }
        }

        natsClient.Close();
    }

    static void ReadYaml()
    {
        var ys = new YamlStream();
        ys.Load(new StreamReader("config.yml"));
        var config = ys.Documents[0].RootNode.ToDynamic();

        foreach (var section in config.sections)
        {
            Console.WriteLine($"{section.title}");

            if (X.HasProperty(section, "endpoints"))
            {
                foreach (var endpoint in section.endpoints)
                {
                    Console.WriteLine($"  {endpoint.subject}");
                }
            }
        }
    }
}

public static class X
{
    // https://stackoverflow.com/questions/28724812/yamldotnet-deserialization-of-deeply-nested-dynamic-structures
    
    public static YamlSequenceNode ToList(this YamlNode node) => node as YamlSequenceNode;
    
    public static YamlMappingNode ToMap(this YamlNode node) => node as YamlMappingNode;
    
    public static dynamic ToDynamic(this YamlNode node, ExpandoObject? exp = default)
    {
        exp ??= new ExpandoObject();
        YamlScalarNode scalar = node as YamlScalarNode;
        YamlMappingNode mapping = node as YamlMappingNode;
        YamlSequenceNode sequence = node as YamlSequenceNode;

        if (scalar != null)
        {
            // TODO: Try converting to double, DateTime and return that.
            string val = scalar.Value;
            return val;
        }
        else if (mapping != null)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
            {
                YamlScalarNode keyNode = (YamlScalarNode)child.Key;
                string keyName = keyNode.Value;
                object val = child.Value.ToDynamic(exp);
                exp.SetProperty(keyName, val);
            }
        }
        else if (sequence != null)
        {
            var childNodes = new List<object>();
            foreach (YamlNode child in sequence.Children)
            {
                var childExp = new ExpandoObject();
                object childVal = child.ToDynamic(childExp);
                childNodes.Add(childVal);
            }

            return childNodes;
        }

        return exp;
    }
    
    public static void SetProperty(this IDictionary<string, object> target, string name, object thing)
    {
        target[name] = thing;
    }
    
    
    public static bool HasProperty(dynamic? obj, string name)
    {
        if (obj == null) return false;
        
        Type objType = obj.GetType();

        if (objType == typeof(ExpandoObject))
        {
            return ((IDictionary<string, object>)obj).ContainsKey(name);
        }

        return objType.GetProperty(name) != null;
    }
}