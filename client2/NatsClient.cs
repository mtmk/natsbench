using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

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
            var state = 1;
            var size = 0;
            var lineStr = String.Empty;
            var sub = "SUB foo 1\r\n";
            
            while (true)
            {
                ReadResult result = await _reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (state == 1 && TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    // Process the line.
                    var cmd = ProcessLine(ref buffer, line, out lineStr);
                    if (cmd == 1)
                    {
                        var memory = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("CONNECT {}\r\n"));
                        while (memory.Length != 0)
                        {
                            var sent = await _socket.SendAsync(memory, SocketFlags.None, CancellationToken.None)
                                .ConfigureAwait(false);
                            memory = memory.Slice(sent);
                        }
                    }
                    else if (cmd == 2)
                    {
                        var memory = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes($"PONG\r\n{sub}"));
                        sub = string.Empty;
                        while (memory.Length != 0)
                        {
                            var sent = await _socket.SendAsync(memory, SocketFlags.None, CancellationToken.None)
                                .ConfigureAwait(false);
                            memory = memory.Slice(sent);
                        }
                    }
                    else if (cmd == 3)
                    {
                        state = 2;
                        size = int.Parse(Regex.Match(lineStr, @"(\d+)$").Groups[1].Value) + 2; // 2=CRLF
                        Console.WriteLine($"MSG SIZE={size}");
                    }
                }
                else if (state == 2 && TryReadPayload(ref buffer, out var payload, size))
                {
                    Console.WriteLine(Encoding.ASCII.GetString(payload));
                    state = 1;
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

    private int ProcessLine(ref ReadOnlySequence<byte> buffer, ReadOnlySequence<byte> line, out string lineStr)
    {
        try
        {
            var s = Encoding.ASCII.GetString(line);
            Console.WriteLine($"[RCV] {s}");

            if (s.StartsWith("INFO"))
            {
                lineStr = string.Empty;
                return 1;
            }

            if (s.StartsWith("PING"))
            {
                lineStr = string.Empty;
                return 2;
            }
            
            if (s.StartsWith("MSG"))
            {
                lineStr = s;
                return 3;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        lineStr = string.Empty;
        return 0;
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
        line = buffer.Slice(0, position.Value.GetInteger() - 1);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    bool TryReadPayload(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> payload, int size)
    {
        if (buffer.Length < size)
        {
            payload = default;
            return false;
        }

        // Skip the line + the \n.
        payload = buffer.Slice(0, size);
        buffer = buffer.Slice(size);
        return true;
    }

    private void LogError(Exception exception)
    {
        Console.WriteLine(exception);
    }
}