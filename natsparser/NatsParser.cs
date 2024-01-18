using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace natsparser;

public struct NatsBytes
{
    private byte[]? _array;
    private int _length;

    public NatsBytes()
    {
        _array = null;
    }
    
    public NatsBytes(ReadOnlySequence<byte> sequence)
    {
        _length = (int) sequence.Length;
        _array = ArrayPool<byte>.Shared.Rent(_length);
        sequence.CopyTo(_array);
    }
    
    public int Length => _length;
    
    public string GetString()
    {
        if (_array == null)
            throw new InvalidOperationException();
        
        return Encoding.ASCII.GetString(_array, 0, _length);
    }
    
    public Span<byte> AsSpan
    {
        get
        {
            if (_array == null)
                throw new InvalidOperationException();
            return _array.AsSpan(0, _length);
        }
    }

    public Memory<byte> AsMemory
    {
        get
        {
            if (_array == null)
                throw new InvalidOperationException();
            return _array.AsMemory(0, _length);
        }
    }

    public void Return()
    {
        if (_array == null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_array);
        _array = null;
        _length = 0;
    }
}

public class NatsParser
{
    private int _currentToken;
    public NatsBytes Subject { get; private set; }
    public NatsBytes ReplyTo { get; private set; }
    public NatsBytes QueueGroup { get; private set; }
    public NatsBytes Sid { get; private set; }
    public NatsBytes Headers { get; private set; }
    public NatsBytes Payload { get; private set; }
    
    public void Reset()
    {
        Subject.Return();
        Subject = default;
        
        Sid.Return();
        Sid = default;
        
        ReplyTo.Return();
        ReplyTo = default;
        
        QueueGroup.Return();
        QueueGroup = default;
        
        Headers.Return();
        Headers = default;
        
        Payload.Return();
        Payload = default;
        
        _currentToken = default;
        Command = default;
        Error = default;
        Json = default;
    }
    
    public string? Error { get; private set; }
    
    public NatsTokenizer.Command Command { get; private set; }
    
    public JsonNode? Json { get; private set; }
    
    public bool TryRead(ref NatsTokenizer tokenizer, ref ReadOnlySequence<byte> buffer)
    {
        while (true)
        {
            var result = tokenizer.Read(ref buffer);

            if (result == NatsTokenizer.Result.ExamineMore)
            {
                continue;
            }

            if (result == NatsTokenizer.Result.Done)
            {
                if (tokenizer.GetCommand() == NatsTokenizer.Command.INFO)
                {
                    var jsonReader = new Utf8JsonReader(tokenizer.GetBufferToken());
                    Json = JsonNode.Parse(ref jsonReader);
                    Command = NatsTokenizer.Command.INFO;
                }
                else if (tokenizer.GetCommand() == NatsTokenizer.Command.PING)
                {
                    Command = NatsTokenizer.Command.PING;
                }
                else if (tokenizer.GetCommand() == NatsTokenizer.Command.PONG)
                {
                    Command = NatsTokenizer.Command.PONG;
                }
                else if (tokenizer.GetCommand() == NatsTokenizer.Command.OK)
                {
                    Command = NatsTokenizer.Command.OK;
                }
                else if (tokenizer.GetCommand() == NatsTokenizer.Command.ERR)
                {
                    Error = Encoding.ASCII.GetString(tokenizer.GetBufferToken()).Trim('\'');
                    Command = NatsTokenizer.Command.ERR;
                }
                
                tokenizer.Reset();
                return true;
            }

            if (result == NatsTokenizer.Result.Token)
            {
                _currentToken++;
                
                // MSG <subject> <sid> [reply-to] <#bytes>␍␊[payload]␍␊
                if (tokenizer.GetCommand() == NatsTokenizer.Command.MSG)
                {
                    Command = NatsTokenizer.Command.MSG;
                    
                    if (tokenizer.IsLastToken)
                    {
                        var length = tokenizer.GetIntegerToken();
                        tokenizer.StartReadSize(length);
                        continue;
                    }
                    if (_currentToken == 1)
                    {
                        Subject = new NatsBytes(tokenizer.GetBufferToken());
                        continue;
                    }
                    if (_currentToken == 2)
                    {
                        Sid = new NatsBytes(tokenizer.GetBufferToken());
                        continue;
                    }
                    if (_currentToken == 3)
                    {
                        ReplyTo = new NatsBytes(tokenizer.GetBufferToken());
                        continue;
                    }
                }
                
                continue;
            }

            if (result == NatsTokenizer.Result.Payload)
            {
                if (tokenizer.GetCommand() == NatsTokenizer.Command.MSG)
                {
                    Payload = new NatsBytes(tokenizer.GetBufferToken());
                    tokenizer.Reset();
                    return true;
                }
            }
            
            if (result == NatsTokenizer.Result.Error)
            {
                throw new Exception("tokenizer error");
            }

            if (result == NatsTokenizer.Result.ReadMore)
            {
                return false;
            }
        }
    }
}