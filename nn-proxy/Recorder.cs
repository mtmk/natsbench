using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

class Recorder
{
    public void Record()
    {
        var listenPort = 8081; //int.Parse(args[0]);
        var connectPort = 8080; //int.Parse(args[1]);

        var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), listenPort);
        listener.Start();
        Console.WriteLine($"Listening on port {listenPort}");

        var channel = Channel.CreateUnbounded<Pkg>();

        var tcs = new TaskCompletionSource();


        Task.Run(async () =>
        {
            try
            {
                await tcs.Task;
                var stopwatch = Stopwatch.StartNew();
                int count = 0;
                await foreach (var pkg in channel.Reader.ReadAllAsync())
                {
                    count++;
                    Console.Error.WriteLine($"{stopwatch.Elapsed} [{count,4}] PKG DIR {pkg.Direction}");
                    var orig = pkg.Direction == 0 ? 'C' : 'S';
                    var data = Convert.ToBase64String(pkg.Data.ToArray());
                    using var sw = new StreamWriter($"d:/tmp/NN-{DateTime.Now:yyyyMMddHHmmss}.dump.txt",
                        Encoding.Latin1, new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write });
                    sw.WriteLine($"{stopwatch.Elapsed} {count} {orig} {data}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });

        while (true)
        {
            TcpClient clientFromClient = listener.AcceptTcpClient();
            TcpClient clientToServer = new TcpClient();
            clientToServer.Connect("127.0.0.1", connectPort);
            Console.WriteLine($"Client connected to {clientToServer.Client.RemoteEndPoint}");

            new Thread(_ =>
            {
                try
                {
                    var buf = new byte[65535].AsSpan();
                    while (true)
                    {
                        var read = clientFromClient.Client.Receive(buf);
                        var span = buf.Slice(0, read);
                        clientToServer.Client.Send(span);
                        var pkg = new Pkg { Data = span.ToArray(), Direction = 0 };
                        channel.Writer.TryWrite(pkg);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    tcs.TrySetResult();
                }
            }).Start();

            new Thread(_ =>
            {
                try
                {
                    var buf = new byte[65535].AsSpan();
                    while (true)
                    {
                        var read = clientToServer.Client.Receive(buf);
                        var span = buf.Slice(0, read);
                        clientFromClient.Client.Send(span);
                        var pkg = new Pkg { Data = span.ToArray(), Direction = 1 };
                        channel.Writer.TryWrite(pkg);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    tcs.TrySetResult();
                }
            }).Start();
        }
    }
    
    struct Pkg
    {
        public Memory<byte> Data { get; set; }
        public int Direction { get; set; }
    }
}