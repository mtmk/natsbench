using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace client2;

class NatsClient
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly Socket _socket;

    private NatsClient(TcpClient tcpClient)
    {
        _socket = tcpClient.Client;

        var pipe = new Pipe();
        _reader = pipe.Reader;
        _writer = pipe.Writer;
    }

    public static NatsClient Connect()
    {
        var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "localhost:4222";
        var host = url.Split(':')[0];
        var port = int.Parse(url.Split(':')[1]);
        
        var tcpClient = new TcpClient();
        tcpClient.Connect(host, port);
        tcpClient.NoDelay = true;
        tcpClient.SendBufferSize = 0;
        tcpClient.ReceiveBufferSize = 0;

        return new NatsClient(tcpClient);
    }

    public NatsClient Start()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                const int minimumBufferSize = 512;

                while (true)
                {
                    // Allocate at least 512 bytes from the PipeWriter.
                    Memory<byte> memory = _writer.GetMemory(minimumBufferSize);
                    try
                    {
                        int bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None);
                        if (bytesRead == 0)
                        {
                            break;
                        }
                        // Tell the PipeWriter how much was read from the Socket.
                        _writer.Advance(bytesRead);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                        break;
                    }

                    // Make the data available to the PipeReader.
                    FlushResult result = await _writer.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                // By completing PipeWriter, tell the PipeReader that there's no more data coming.
                await _writer.CompleteAsync();
            }

        });

        Task.Run(async () =>
        {
            while (true)
            {
                ReadResult result = await _reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    await ProcessLine(line);
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                _reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete.
            await _reader.CompleteAsync();
        });
        
        return this;
    }

    private async Task ProcessLine(ReadOnlySequence<byte> line)
    {
        try
        {
            var s = Encoding.ASCII.GetString(line);
            Console.WriteLine($"[RCV] {s}");

            if (s.StartsWith("INFO"))
            {
                var memory = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("CONNECT {}\r\n"));
                while (memory.Length != 0)
                {
                    var sent = await _socket.SendAsync(memory, SocketFlags.None, CancellationToken.None)
                        .ConfigureAwait(false);
                    memory = memory.Slice(sent);
                }
            }

            if (s.StartsWith("PING"))
            {
                var memory = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("PONG\r\n"));
                while (memory.Length != 0)
                {
                    var sent = await _socket.SendAsync(memory, SocketFlags.None, CancellationToken.None)
                        .ConfigureAwait(false);
                    memory = memory.Slice(sent);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        // Look for a EOL in the buffer.
        SequencePosition? position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        // Skip the line + the \n.
        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }
    
    private void LogError(Exception exception)
    {
        Console.WriteLine(exception);
    }
}