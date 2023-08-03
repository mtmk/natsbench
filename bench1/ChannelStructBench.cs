using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace bench1;

/*
|       Method |     Mean |    Error |   StdDev |   Gen0 |   Gen1 | Allocated |
|------------- |---------:|---------:|---------:|-------:|-------:|----------:|
|  WithClass32 | 56.19 ns | 1.142 ns | 1.525 ns | 0.0087 | 0.0012 |     120 B |
| WithStruct16 | 52.65 ns | 0.571 ns | 0.535 ns | 0.0052 |      - |      72 B |
| WithStruct24 | 51.67 ns | 0.599 ns | 0.560 ns | 0.0052 |      - |      72 B |
| WithStruct32 | 54.01 ns | 0.774 ns | 0.686 ns | 0.0052 |      - |      72 B |
| WithStruct64 | 57.69 ns | 1.162 ns | 1.590 ns | 0.0052 |      - |      72 B |
*/
[MemoryDiagnoser]
public class ChannelStructBench
{
    private Channel<ChannelStructBenchClass4> _cc4;
    private Channel<ChannelStructBenchStruct2> _cs2;
    private Channel<ChannelStructBenchStruct3> _cs3;
    private Channel<ChannelStructBenchStruct4> _cs4;
    private Channel<ChannelStructBenchStruct8> _cs8;
    private int _i;

    [GlobalSetup]
    public async Task Setup()
    {
        _cc4 = Channel.CreateBounded<ChannelStructBenchClass4>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _cs2 = Channel.CreateBounded<ChannelStructBenchStruct2>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _cs3 = Channel.CreateBounded<ChannelStructBenchStruct3>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _cs4 = Channel.CreateBounded<ChannelStructBenchStruct4>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _cs8 = Channel.CreateBounded<ChannelStructBenchStruct8>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });    }
    
    [Benchmark]
    public async Task<int> WithClass32()
    {
        await _cc4.Writer.WriteAsync(new ChannelStructBenchClass4(1, 2, 3, 4));
        return Interlocked.Increment(ref _i);
    }

    [Benchmark]
    public async Task<int> WithStruct16()
    {
        await _cs2.Writer.WriteAsync(new ChannelStructBenchStruct2());
        return Interlocked.Increment(ref _i);
    }

    [Benchmark]
    public async Task<int> WithStruct24()
    {
        await _cs3.Writer.WriteAsync(new ChannelStructBenchStruct3());
        return Interlocked.Increment(ref _i);
    }

    [Benchmark]
    public async Task<int> WithStruct32()
    {
        await _cs4.Writer.WriteAsync(new ChannelStructBenchStruct4());
        return Interlocked.Increment(ref _i);
    }
    
    [Benchmark]
    public async Task<int> WithStruct64()
    {
        await _cs8.Writer.WriteAsync(new ChannelStructBenchStruct8());
        return Interlocked.Increment(ref _i);
    }
}

record class ChannelStructBenchClass4(long F1, long F2, long F3, long F4);

record struct ChannelStructBenchStruct2(long F1, long F2);
record struct ChannelStructBenchStruct3(long F1, long F2, long F3);
record struct ChannelStructBenchStruct4(long F1, long F2, long F3, long F4);

record struct ChannelStructBenchStruct8(long F1, long F2, long F3, long F4, long F5, long F6, long F7, long F8);
