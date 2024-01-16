using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace natsparser.test;

public class NatsParserTest(ITestOutputHelper Ouptput)
{
    [Fact]
    public void T()
    {
        var sequences = new List<ReadOnlySequence<byte>>
        {
            new SequenceBuilder()
                .Append("INFO {\"server_id\":\"nats-server\""u8.ToArray())
                .Append("}\r"u8.ToArray())
                .Append("\nPI"u8.ToArray())
                .ReadOnlySequence,
            new SequenceBuilder()
                .Append("NG"u8.ToArray())
                .Append("\r"u8.ToArray())
                .Append("\n"u8.ToArray())
                .Append("PO"u8.ToArray())
                .ReadOnlySequence,
            new SequenceBuilder()
                .Append("NG\r\n"u8.ToArray())
                .Append("+OK\r\n"u8.ToArray())
                .Append("-ER"u8.ToArray())
                .Append("R 'cra"u8.ToArray())
                .Append("sh!'\r\nPI"u8.ToArray())
                .Append("NG\r\n"u8.ToArray())
                .ReadOnlySequence,
            new SequenceBuilder()
                .Append("MSG subject sid1 reply_to 1\r\nx\r\n"u8.ToArray())
                .ReadOnlySequence,
            new SequenceBuilder()
                .Append("PING\r\n"u8.ToArray())
                .ReadOnlySequence,
        };

        var parser = new NatsParser();

        foreach (var sequence in sequences)
        {
            var buffer = sequence;
            
            while (true)
            {
                var result = parser.Read(ref buffer);

                if (result == NatsParser.Result.ExamineMore)
                {
                    continue;
                }

                if (result == NatsParser.Result.Done)
                {
                    if (parser.GetCommand() == NatsParser.Command.INFO)
                    {
                        var jsonReader = new Utf8JsonReader(parser.GetBufferToken());
                        var json = JsonNode.Parse(ref jsonReader);

                        Ouptput.WriteLine($"INFO {json}");
                    }
                    else if (parser.GetCommand() == NatsParser.Command.PING)
                    {
                        Ouptput.WriteLine($"PING");
                    }
                    else if (parser.GetCommand() == NatsParser.Command.PONG)
                    {
                        Ouptput.WriteLine($"PONG");
                    }
                    else if (parser.GetCommand() == NatsParser.Command.OK)
                    {
                        Ouptput.WriteLine($"+OK");
                    }
                    else if (parser.GetCommand() == NatsParser.Command.ERR)
                    {
                        var error = Encoding.ASCII.GetString(parser.GetBufferToken());
                        Ouptput.WriteLine($"-ERR {error}");
                    }
                    else if (parser.GetCommand() == NatsParser.Command.MSG)
                    {
                        var error = Encoding.ASCII.GetString(parser.GetBufferToken());
                        Ouptput.WriteLine($"-ERR {error}");
                    }
                    
                    parser.Reset();
                    continue;
                }

                if (result == NatsParser.Result.Token)
                {
                    if (parser.GetCommand() == NatsParser.Command.MSG)
                    {
                        if (parser.IsLastToken)
                        {
                            var length = parser.GetIntegerToken();
                            Ouptput.WriteLine($"MSG length={length}");
                            parser.StartReadSize(length);
                            continue;
                        }
                        else
                        {
                            var token = Encoding.ASCII.GetString(parser.GetBufferToken());
                            Ouptput.WriteLine($"MSG token={token}");
                        }
                    }
                    
                    if (parser.IsLastToken)
                        parser.Reset();
                    
                    continue;
                }

                if (result == NatsParser.Result.Payload)
                {
                    var payload = Encoding.ASCII.GetString(parser.GetBufferToken());
                    Ouptput.WriteLine($"MSG payload={payload}");
                    parser.Reset();
                    continue;
                }
                
                if (result == NatsParser.Result.Error)
                {
                    Ouptput.WriteLine("ERROR");
                    break;
                }

                if (result == NatsParser.Result.ReadMore)
                {
                    break;
                }
            }
        }
    }
}

    class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public void SetMemory(ReadOnlyMemory<byte> memory) => Memory = memory;

        public void SetNextSegment(BufferSegment? segment) => Next = segment;

        public void SetRunningIndex(int index) => RunningIndex = index;
    }
    
    class SequenceBuilder
    {
        private BufferSegment? _start;
        private BufferSegment? _end;
        private int _length;

        public ReadOnlySequence<byte> ReadOnlySequence => new(_start!, 0, _end!, _end!.Memory.Length);

        // Memory is only allowed rent from ArrayPool.
        public SequenceBuilder Append(ReadOnlyMemory<byte> buffer)
        {
            var segment = new BufferSegment();
            segment.SetMemory(buffer);
            
            if (_start == null)
            {
                _start = segment;
                _end = segment;
            }
            else
            {
                _end!.SetNextSegment(segment);
                segment.SetRunningIndex(_length);
                _end = segment;
            }

            _length += buffer.Length;
            
            return this;
        }
    }