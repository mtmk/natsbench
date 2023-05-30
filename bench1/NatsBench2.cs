using BenchmarkDotNet.Attributes;
using NATS.Client.Core;

namespace bench1;

[MemoryDiagnoser]
public class NatsBench2
{
    private NatsConnection _nats;
    private ManualResetEventSlim _signal;
    private int _count;
    private string _message;

    [GlobalSetup]
    public async Task Setup()
    {
        _nats = new NatsConnection();
        _signal = new ManualResetEventSlim();

        _count = 0;

        var sub = await _nats.SubscribeAsync("foo");

        _ = Task.Run(async () =>
        {
            await foreach (var msg in sub.Msgs.ReadAllAsync())
            {
                Interlocked.Increment(ref _count);
                _signal.Set();
            }
        });

        _message = "my_message";
    }
    
    [Benchmark]
    public async Task<int> PubSub()
    {
        await _nats.PublishAsync("foo", _message);
        _signal.Wait();
        _signal.Reset();
        return Volatile.Read(ref _count);
    }}