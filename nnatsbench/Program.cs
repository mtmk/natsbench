using System.Diagnostics;
using System.Buffers;

// using AlterNats;
using NATS.Client.Core;

var subject = "foo";
var msgs = 1_000;
var size = 128;
Console.WriteLine($"Starting [subject={subject}, msgs={msgs:n0}, msgsize={size}]");

// await using var nats1 = new AlterNats.NatsConnection();
// await nats1.PingAsync();
// await using var nats2 = new AlterNats.NatsConnection();
// await nats2.PingAsync();
// int i = 0;
// var r = new ManualResetEventSlim();
// var natsKey = new NatsKey(subject);
// nats1.SubscribeAsync(natsKey, () =>
// {
//     var ii = Interlocked.Increment(ref i);
//     if (ii == msgs) r.Set();
// });
// var bytes = new byte[size];
// for (int j = 0; j < msgs; j++)
// {
//     nats2.PostPublish(natsKey, bytes);
// }
// r.Wait();



await using var nats1 = new NatsConnection();
await nats1.PingAsync();
await using var nats2 = new NatsConnection();
await nats2.PingAsync();
Thread.Sleep(1000);
await using var sub = await nats1.SubscribeAsync(subject);
var stopwatch = Stopwatch.StartNew();
var subReader = Task.Run(async () =>
{
     var count = 0;
     await foreach (var msg in sub.Msgs.ReadAllAsync())
     {
         if (++count == msgs) break;
     }
     await sub.UnsubscribeAsync();
});
var payload = new ReadOnlySequence<byte>(new byte[size]);
for (int i = 0; i < msgs; i++)
{
    nats2.PubAsync(subject, payload: payload);
}
await subReader;
await sub.UnsubscribeAsync();



var totalSeconds = stopwatch.Elapsed.TotalSeconds;
Console.WriteLine($"pub/sub stats: {2 * msgs / totalSeconds:n0} msgs/sec ~ {2 * msgs * size / (1024.0 * 1024.0) / totalSeconds:n2} MB/sec");