using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace bench1;

/*
|           Method |     Mean |    Error |   StdDev |   Median | Allocated |
|----------------- |---------:|---------:|---------:|---------:|----------:|
|   UsingByteArray | 10.51 ns | 0.240 ns | 0.267 ns | 10.40 ns |         - |
| UsingStringBytes | 12.37 ns | 0.151 ns | 0.134 ns | 12.33 ns |         - |
|   UsingCharArray | 17.57 ns | 0.383 ns | 0.711 ns | 17.27 ns |         - |
|      UsingString | 19.83 ns | 0.155 ns | 0.145 ns | 19.76 ns |         - |
*/

[MemoryDiagnoser]
public class NatsKeyBench
{ 
    private ArrayBufferWriter<byte>? _buffer;
    private byte[]? _byteArray;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(128);
        _byteArray = new byte[10];
    }
    
    [Benchmark]
    public int UsingByteArray()
    {
        return WriteBytes(_byteArray!);
    }

    [Benchmark]
    public int UsingCharArray()
    {
        return WriteChars("1234567890");
    }
    
    [Benchmark]
    public int UsingStringBytes()
    {
        return WriteStringBytes("1234567890");
    }
    
    [Benchmark]
    public int UsingString()
    {
        return WriteString("1234567890");
    }
    
    private int WriteBytes(byte[] key)
    {
        _buffer!.Write(key.AsSpan());
        var count = _buffer!.WrittenCount;
        _buffer.Clear();
        return count;
    }
    
    private int WriteChars(ReadOnlySpan<char> key)
    {
        const int maxLocalBufferSize = 64;
        Span<byte> localBuffer = stackalloc byte[maxLocalBufferSize];
        Span<byte> input = localBuffer[..key.Length];
        for (var index = 0; index < key.Length; index++)
        {
            char c = key[index];
            input[index] = (byte)c;
        }
        _buffer!.Write(input);
        var count = _buffer!.WrittenCount;
        _buffer!.Clear();
        return count;
    }
    
    private int WriteStringBytes(string key)
    {
        var count = key.Length;
        Span<byte> input = _buffer!.GetSpan(count);
        for (var index = 0; index < count; index++)
        {
            char c = key[index];
            input[index] = (byte)c;
        }
        _buffer!.Advance(count);
        _buffer!.Clear();
        return count;
    }
    
    private int WriteString(string key)
    {
        var count =  (int)Encoding.ASCII.GetBytes(key, _buffer!);
        _buffer!.Clear();
        return count;
    }
}