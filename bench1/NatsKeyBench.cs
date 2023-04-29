using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace bench1;

[MemoryDiagnoser]
public class NatsKeyBench
{
    private ArrayBufferWriter<byte> _buffer;
    private byte[] _byteArray;
    private ReadOnlySpan<char> _charArray;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>();
        _byteArray = new byte[10];
        _charArray = "1234567890";
    }
    
    [Benchmark]
    public int UsingByteArray()
    {
_buffer.Write();
    }

    private int WriteChars(ReadOnlySpan<char> key)
    {
        key.
        _buffer.Write();
    }
}