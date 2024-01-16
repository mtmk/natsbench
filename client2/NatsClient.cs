using System.Buffers;
using System.Buffers.Binary;
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
                        int read = await _socket.ReceiveAsync(memory, SocketFlags.None);
                        if (read == 0)
                        {
                            break;
                        }
                        // Tell the PipeWriter how much was read from the Socket.
                        _writer.Advance(read);
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
                ReadResult result = await _reader.ReadAtLeastAsync(2);
                
                if (result.IsCanceled)
                    break;
                
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition consumed = buffer.Start;
                
                try
                {
                    var cmd = ReadCmd(ref buffer);
                    
                    if (cmd == CmdPre.INFO)
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss} [RCV] INFO");
                    }

                    consumed = buffer.Start;
                }
                finally
                {
                    _reader.AdvanceTo(consumed);
                }
                
                if (result.IsCompleted)
                    break;
            }

            // Mark the PipeReader as complete.
            await _reader.CompleteAsync();
        });
        
        return this;
    }

    short ReadCmd(ref ReadOnlySequence<byte> buffer)
    {
        short cmd;
        if (buffer.IsSingleSegment)
        {
            cmd = BinaryPrimitives.ReadInt16LittleEndian(buffer.First.Span);
        }
        else
        {
            Span<byte> b1 = stackalloc byte[2];
            buffer.Slice(0, 2).CopyTo(b1);
            cmd = BinaryPrimitives.ReadInt16LittleEndian(b1);
        }
        
        buffer = buffer.Slice(0, 2);
        
        return cmd;
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

public static class CmdPre
{
    public const short OK = 20267;
    public const short ERR = 17709;
    public const short CONNECT = 20291;
    public const short HMSG = 19784;
    public const short HPUB = 20552;
    public const short INFO = 20041;
    public const short MSG = 21325;
    public const short PING = 18768;
    public const short PONG = 20304;
    public const short PUB = 21840;
    public const short SUB = 21843;
    public const short UNSUB = 20053;
}
