using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace client2;

class Program
{
    static void Main()
    {
        // DumpPre(); return;
        
        var nats = NatsClient.Connect().Start();

        while (true)
        {
            var cmd = Console.ReadLine();

            if (Regex.IsMatch(cmd, @"^\s*q(uit)?\s*$"))
            {
                break;
            }
        }

        Console.WriteLine("bye");
    }

    static void DumpPre()
    {
        Console.WriteLine("public static class CmdPre");
        Console.WriteLine("{");
        var hashSet = new HashSet<string>();
        foreach (var k in new[] { "+OK", "-ERR", "CONNECT", "HMSG", "HPUB", "INFO", "MSG", "PING", "PONG", "PUB", "SUB", "UNSUB" })
        {
            var pStr = k.Substring(0, 2);
            if (!hashSet.Add(pStr))
            {
                throw new Exception($"duplicate \"{pStr}\":");
            }

            var p = BinaryPrimitives.ReadInt16LittleEndian(pStr.Select(c => (byte)c).ToArray());
            var n = k.Replace("-", "").Replace("+", "");
            Console.WriteLine($"    public const short {n} = {p};");
        }
        Console.WriteLine("}");
    }
}