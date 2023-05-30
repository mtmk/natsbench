
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

var port1 = 4333;
var port2 = 4222;
var tcpListener = new TcpListener(IPAddress.Loopback, port1);
tcpListener.Start();
Console.WriteLine($"Listing on {port1}");
var client = 0;
while (true)
{
    var tcpClient1 = tcpListener.AcceptTcpClient();
    
    var tcpClient2 = new TcpClient("127.0.0.1", port2);

    var n = ++client;
    
    Console.WriteLine($"Client [{n}] connected to {port2}");
    
    #pragma warning disable CS4014
    Task.Run(() =>
    {
        var stream1 = tcpClient1.GetStream();
        var sr1 = new StreamReader(stream1, Encoding.ASCII);
        var sw1 = new StreamWriter(stream1, Encoding.ASCII);

        var stream2 = tcpClient2.GetStream();
        var sr2 = new StreamReader(stream2, Encoding.ASCII);
        var sw2 = new StreamWriter(stream2, Encoding.ASCII);

        Task.Run(() =>
        {
            while (NatsProtoDump($"[{n}] <--", sr1, sw2)) {}
        });
        while (NatsProtoDump($"[{n}] -->", sr2, sw1)) {}
    });

}

bool NatsProtoDump(string dir, StreamReader sr, StreamWriter sw)
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
