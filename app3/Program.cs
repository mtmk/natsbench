//
// Simple NATS server implementation just implements PING
//
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

// ReSharper disable UnusedVariable
// ReSharper disable FunctionNeverReturns
#pragma warning disable CS1998

var tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 4222);
tcpListener.Start();

var port = ((IPEndPoint)tcpListener.Server.LocalEndPoint!).Port;

Console.WriteLine($"Server started on port {port}");

while (true)
{
    var tcpClient = await tcpListener.AcceptTcpClientAsync();

    Console.WriteLine($"New client connected");

    _ = Task.Run(async delegate
    {
        var networkStream = tcpClient.GetStream();
        var channel = Channel.CreateUnbounded<string>();

        // Initial server response
        await channel.Writer.WriteAsync("INFO {}\r\n");
        
        // Read loop
        var readerTask = Task.Run(async delegate
        {
            using var reader = new StreamReader(networkStream);
            while (true)
            {
                var message = await reader.ReadLineAsync();
                if (message == null) break;
                DumpFill("RX", message);
                if (message.StartsWith("PING"))
                {
                    await channel.Writer.WriteAsync($"PONG\r\n");
                }
            }
        });

        // Write loop
        var writerTask = Task.Run(async delegate
        {
            await using var writer = new StreamWriter(networkStream);
            await foreach (var message in channel.Reader.ReadAllAsync())
            {
                await writer.WriteAsync(message);
                await writer.FlushAsync();
                DumpFill("TX", message);
            }
        });

        await writerTask;
        await readerTask;

        Console.WriteLine("Closing client");
    }).ContinueWith(t => Console.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
}

void DumpFill(string dir, ReadOnlySpan<char> span)
{
    var sb = new StringBuilder();
    foreach (var c in span)
    {
        switch (c)
        {
            case > ' ' and <= '~':
                sb.Append(c);
                break;
            case ' ':
                sb.Append('␠');
                break;
            case '\t':
                sb.Append('␋');
                break;
            case '\n':
                sb.Append('␊');
                break;
            case '\r':
                sb.Append('␍');
                break;
            default:
                sb.Append('.');
                break;
        }
    }
    Console.WriteLine($"[{dir}] {sb}");
}