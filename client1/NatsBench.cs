using BenchmarkDotNet.Attributes;

namespace client1;

[MemoryDiagnoser]
public class NatsBench
{ 
    private NatsClient _nats;
    private ManualResetEventSlim _signal;
    private int _count;
    private string _message;

    [GlobalSetup]
    public void Setup()
    {
        _nats = NatsClient.Connect();
        _signal = new ManualResetEventSlim();

        _count = 0;
        
        _nats.Sub("foo", "", m =>
        {
            Interlocked.Increment(ref _count);
            _signal.Set();
        });

        _message = "my_message";
    }
    
    [Benchmark]
    public int PubSub()
    {
        _nats.Pub("foo", "", _message);
        _signal.Wait();
        _signal.Reset();
        return Volatile.Read(ref _count);
    }
}