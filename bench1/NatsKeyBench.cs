using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace bench1;

/*
|         Method |      Mean |     Error |    StdDev | Allocated |
|--------------- |----------:|----------:|----------:|----------:|
| UsingByteArray |  7.225 ns | 0.1626 ns | 0.1808 ns |         - |
| UsingCharArray | 12.663 ns | 0.1375 ns | 0.1286 ns |         - |
|    UsingString | 14.131 ns | 0.1340 ns | 0.1254 ns |         - |
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
    
    private int WriteString(string key)
    {
        var count =  (int)Encoding.ASCII.GetBytes(key, _buffer!);
        _buffer!.Clear();
        return count;
    }
}