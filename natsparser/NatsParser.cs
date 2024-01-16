using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;

namespace natsparser;

/*
B: PING␍␊
B: PONG␍␊
S: +OK␍␊
S: -ERR <error message>␍␊ 
S: INFO {"option_name":option_value,...}␍␊
S: MSG <subject> <sid> [reply-to] <#bytes>␍␊[payload]␍␊
S: HMSG <subject> <sid> [reply-to] <#header-bytes> <#total-bytes>␍␊[headers]␍␊␍␊[payload]␍␊

C: CONNECT {"option_name":option_value,...}␍␊
C: SUB <subject> [queue group] <sid>␍␊
C: UNSUB <sid> [max_msgs]␍␊
C: PUB <subject> [reply-to] <#bytes>␍␊[payload]␍␊
C: HPUB <subject> [reply-to] <#header-bytes> <#total-bytes>␍␊[headers]␍␊␍␊[payload]␍␊
*/

public ref struct NatsParser
{
    public enum Result : byte { ReadMore, ExamineMore, Done, Token, Payload, Error }
    
    public enum Command : byte { NONE, OK, ERR, CONNECT, HMSG, HPUB, INFO, MSG, PING, PONG, PUB, SUB, UNSUB }

    enum State { Start, Cmd, End }
    
    private int _size;
    private bool _isLastToken;
    private short _tokenIndex;
    private Command _cmd;
    private State _state;
    private ReadOnlySequence<byte> _buffer;

    public NatsParser() => Reset();

    public void Reset()
    {
        _buffer = default;
        _cmd = default;
        _isLastToken = default;
        _size = default;
        _state = default;
        _tokenIndex = default;
    }

    public bool IsLastToken => _isLastToken;
    
    public short GetTokenIndex() => _tokenIndex;
    
    public Command GetCommand() => _cmd;
    
    public ReadOnlySequence<byte> GetBufferToken() => _buffer;

    public int GetIntegerToken()
    {
        if (_buffer.Length > 10)
            throw new Exception("number too long");
        
        Span<byte> span = stackalloc byte[(int)_buffer.Length];
        _buffer.CopyTo(span);

        if (!Utf8Parser.TryParse(span, out int value, out _))
        {
            throw new Exception("number format error");
        }

        return value;
    }
    
    public Result Read(ref ReadOnlySequence<byte> buffer)
    {
        if (_size > 0)
        {
            if (buffer.Length < _size + 2)
            {
                return Result.ReadMore;
            }

            _buffer = buffer.Slice(0, _size);
            buffer = buffer.Slice(_size + 2);
            _size = 0;
            return Result.Payload;
        }
        
        if (_state == State.Start)
        {
            if (buffer.Length < 2)
            {
                return Result.ReadMore;
            }

            var readShort = ReadShort(ref buffer);

            if (readShort == CommandShort.INFO)
            {
                _cmd = Command.INFO;
                _state = State.Cmd;
                return Result.ExamineMore;
            }
            
            if (readShort == CommandShort.PING)
            {
                _cmd = Command.PING;
                _state = State.Cmd;
                return Result.ExamineMore;
            }

            if (readShort == CommandShort.PONG)
            {
                _cmd = Command.PONG;
                _state = State.Cmd;
                return Result.ExamineMore;
            }
            
            if (readShort == CommandShort.OK)
            {
                _cmd = Command.OK;
                _state = State.Cmd;
                return Result.ExamineMore;
            }
            
            if (readShort == CommandShort.ERR)
            {
                _cmd = Command.ERR;
                _state = State.Cmd;
                return Result.ExamineMore;
            }
            
            if (readShort == CommandShort.MSG)
            {
                _cmd = Command.MSG;
                _state = State.Cmd;
                return Result.ExamineMore;
            }
            
            return Result.Error;
        }

        if (_state == State.Cmd)
        {
            if (_cmd == Command.INFO)
            {
                if (_tokenIndex == 0)
                {
                    // IN FO.
                    if (buffer.Length < 3)
                    {
                        return Result.ReadMore;
                    }

                    buffer = buffer.Slice(3);
                    _tokenIndex = 1;
                    return Result.ExamineMore;
                }

                if (_tokenIndex == 1)
                {
                    var positionOfNewLine = buffer.PositionOf((byte)'\n');
                    if (positionOfNewLine == null)
                        return Result.ReadMore;

                    _buffer = buffer.Slice(0, positionOfNewLine.Value);
                    _isLastToken = true;
                    
                    buffer = buffer.Slice(positionOfNewLine.Value).Slice(1);

                    return Result.Done;
                }

                return Result.Error;
            }

            if (_cmd == Command.PING)
            {
                // PI NG..
                if (buffer.Length < 4)
                {
                    return Result.ReadMore;
                }
                
                buffer = buffer.Slice(4);
                return Result.Done;
            }
            
            if (_cmd == Command.PONG)
            {
                // PO NG..
                if (buffer.Length < 4)
                {
                    return Result.ReadMore;
                }
                
                buffer = buffer.Slice(4);
                return Result.Done;
            }

            if (_cmd == Command.OK)
            {
                // +O K..
                if (buffer.Length < 3)
                {
                    return Result.ReadMore;
                }
                
                buffer = buffer.Slice(3);
                return Result.Done;
            }
            
            if (_cmd == Command.ERR)
            {
                if (_tokenIndex == 0)
                {
                    // -ER R.
                    if (buffer.Length < 3)
                    {
                        return Result.ReadMore;
                    }

                    buffer = buffer.Slice(3);
                    _tokenIndex = 1;
                    return Result.ExamineMore;
                }
                
                if (_tokenIndex == 1)
                {
                    var positionOfNewLine = buffer.PositionOf((byte)'\n');
                    if (positionOfNewLine == null)
                        return Result.ReadMore;

                    _buffer = buffer.Slice(0, positionOfNewLine.Value);
                    
                    // Trim last \r
                    if (_buffer.Length > 0)
                        _buffer = buffer.Slice(0, _buffer.Length - 1);
                    
                    _isLastToken = true;
                    
                    buffer = buffer.Slice(positionOfNewLine.Value);
                    
                    // Trim last \n
                    if (buffer.Length > 0)
                        buffer = buffer.Slice(1);

                    return Result.Done;
                }
            }

            if (_cmd == Command.MSG)
            {
                if (_tokenIndex == 0)
                {
                    // MS G.
                    if (buffer.Length < 2)
                    {
                        return Result.ReadMore;
                    }

                    buffer = buffer.Slice(2);
                    _tokenIndex = 1;
                    return Result.ExamineMore;
                }

                if (_tokenIndex > 0)
                {
                    _tokenIndex++;
                    
                    var position = buffer.PositionOf((byte)' ');
                    if (position == null)
                    {
                        position = buffer.PositionOf((byte)'\n');
                        if (position == null)
                            return Result.ReadMore;
                        
                        _isLastToken = true;
                    }
                    
                    _buffer = buffer.Slice(0, position.Value);
                    
                    // Trim last \r
                    if (_isLastToken && _buffer.Length > 0)
                        _buffer = buffer.Slice(0, _buffer.Length - 1);
                    
                    buffer = buffer.Slice(position.Value);
                    
                    // Trim last space
                    if (buffer.Length > 0)
                        buffer = buffer.Slice(1);

                    return Result.Token;
                }
            }
        }

        return Result.Error;
    }

    short ReadShort(ref ReadOnlySequence<byte> buffer)
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
        
        buffer = buffer.Slice(2);
        
        return cmd;
    }
    
    public static class CommandShort
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

    public void StartReadSize(int size)
    {
        _size = size;
    }
}