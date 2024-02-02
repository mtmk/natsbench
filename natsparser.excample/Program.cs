using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using natsparser;

ConcurrentQueue<PingCommand> _pingCommands = new();
    
var tcpClient = new TcpClient();
tcpClient.NoDelay = true;
// tcpClient.SendBufferSize = 8192;
// tcpClient.ReceiveBufferSize = 8192;
tcpClient.Connect(Environment.GetEnvironmentVariable("NATS_URL") ?? "127.0.0.1", 4222);

Console.WriteLine("tcp connect");

var pipeRcv = new Pipe(new PipeOptions(
    readerScheduler:PipeScheduler.Inline,
    writerScheduler:PipeScheduler.Inline,
    useSynchronizationContext:false));
var readerRcv = pipeRcv.Reader;
var writerRcv = pipeRcv.Writer;

var pipeSnd = new Pipe(new PipeOptions(useSynchronizationContext:false));
var readerSnd = pipeSnd.Reader;
var writerSnd = pipeSnd.Writer;

Task.Run(async () =>
{
    try
    {
        while (true)
        {
            // Console.WriteLine($"RCV");
            var memory = writerRcv.GetMemory();
            var read = await tcpClient.Client.ReceiveAsync(memory, SocketFlags.None);
            // Console.WriteLine($"RCVed {read}");
            writerRcv.Advance(read);
            await writerRcv.FlushAsync();
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"RCV WRITE LOOP ERROR: {e}");
    }
});

var ready = new ManualResetEventSlim();
Task.Run(async () =>
{
    var parser = new NatsParser();
    var tokenizer = new NatsTokenizer();
    try
    {
        while (true)
        {
            var result = await readerRcv.ReadAsync();
            var buffer = result.Buffer;
            if (!buffer.IsEmpty)
            {
                while (parser.TryRead(ref tokenizer, ref buffer))
                {
                    Console.WriteLine($"Command: {parser.Command}");
                    if (parser.Command == NatsTokenizer.Command.MSG)
                    {
                        Console.WriteLine($"  subject: {parser.Subject.GetString()}");
                        Console.WriteLine($"  sid: {parser.Sid.GetString()}");
                        Console.WriteLine($"  reply-to: {parser.ReplyTo.GetString()}");
                        Console.WriteLine($"  Payload-Length: {parser.Payload.Length}");
                        Console.WriteLine($"  Payload: {parser.Payload.GetString()}");
                    }
                    else if (parser.Command == NatsTokenizer.Command.INFO)
                    {
                        Console.WriteLine($"  JSON: {parser.Json}");
                        await tcpClient.Client.SendAsync("CONNECT {\"verbose\":false}\r\n"u8.ToArray(), SocketFlags.None);
                        await tcpClient.Client.SendAsync("PING\r\n"u8.ToArray(), SocketFlags.None);
                    }
                    else if (parser.Command == NatsTokenizer.Command.PONG)
                    {
                        ready.Set();
                        if (_pingCommands.TryDequeue(out var pingCommand))
                        {
                            Console.WriteLine("PING COMMAND - PONG");
                            pingCommand.TaskCompletionSource.SetResult(DateTimeOffset.UtcNow - pingCommand.WriteTime);
                        }
                    }
                    else if (parser.Command == NatsTokenizer.Command.PING)
                    {
                        await tcpClient.Client.SendAsync("PONG\r\n"u8.ToArray(), SocketFlags.None);
                    }
                    
                    parser.Reset();
                }
                readerRcv.AdvanceTo(buffer.Start);
            }

            if (result.IsCompleted)
            {
                break;
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"SND WRITE LOOP ERROR: {e}");
    }
});

Task.Run(async () =>
{
    try
    {
        while (true)
        {
            var result = await readerSnd.ReadAsync();
            var buffer = result.Buffer;
            if (!buffer.IsEmpty)
            {
                var read = buffer.Length;
                var bytes = ArrayPool<byte>.Shared.Rent((int)read);
                var memory = new Memory<byte>(bytes).Slice(0, (int)read);
                buffer.CopyTo(memory.Span);
                var write = await tcpClient.Client.SendAsync(memory, SocketFlags.None);
                // Console.WriteLine($"SND {read} {write}");
                ArrayPool<byte>.Shared.Return(bytes);
                readerSnd.AdvanceTo(buffer.End);
            }
        }
    }
    catch(Exception e)
    {
        Console.WriteLine($"SND WRITE LOOP ERROR: {e}");
    }
});

ready.Wait();
Console.WriteLine("Connected!");

var payload = Encoding.ASCII.GetString(new byte[128]);
var pub = Encoding.ASCII.GetBytes($"PUB x {payload.Length}\r\n{payload}\r\n").AsMemory();
var ping = Encoding.ASCII.GetBytes($"PING\r\n").AsMemory();
var len = pub.Length;
var stopwatch = Stopwatch.StartNew();
var max = int.Parse(args[0]);
for (int i = 0; i < max; i++)
{
    // Console.ReadLine();
    var buffer = writerSnd.GetMemory(len);
    pub.CopyTo(buffer);
    writerSnd.Advance(len);
    await writerSnd.FlushAsync();
    // await Task.Delay(1000);
}

{
    var pingCommand = new PingCommand();
    _pingCommands.Enqueue(pingCommand);
    Console.WriteLine("PING COMMAND - ENQUEUED");
   
    var buffer = writerSnd.GetMemory(ping.Length);
    ping.CopyTo(buffer);
    writerSnd.Advance(ping.Length);
    await writerSnd.FlushAsync();
    
    Console.WriteLine("PING COMMAND - WAIT RESULT");
    var timeSpan = await pingCommand.TaskCompletionSource.Task.ConfigureAwait(false);
    Console.WriteLine($"PING COMMAND - RESULT RTT: {timeSpan.TotalMilliseconds:n0}ms");
}

Console.WriteLine($"took {5000000/stopwatch.Elapsed.TotalSeconds:n0} msgs/s");

internal struct PingCommand
{
    public PingCommand()
    {
    }

    public DateTimeOffset WriteTime { get; } = DateTimeOffset.UtcNow;

    public TaskCompletionSource<TimeSpan> TaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}