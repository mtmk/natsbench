using System.Diagnostics;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

// using AlterNats;
using NATS.Client.Core;

var subject = "foo";
var msgs = 1_000_000;
var size = 128;
Console.WriteLine($"Starting [subject={subject}, msgs={msgs:n0}, msgsize={size}]");



// await using var nats1 = new NATS.Client.Core.NatsConnection();
// await nats1.PingAsync();
// await using var nats2 = new NATS.Client.Core.NatsConnection();
// await nats2.PingAsync();
// int i = 0;
// var r = new ManualResetEventSlim();
// var natsKey = new NATS.Client.Core.NatsKey(subject);
// var stopwatch = Stopwatch.StartNew();
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



// await using var nats1 = new NATS.Client.Core.NatsConnection();
// await nats1.PingAsync();
// await using var nats2 = new NATS.Client.Core.NatsConnection();
// await nats2.PingAsync();
// var stopwatch = Stopwatch.StartNew();
// var sub = await nats1.SubscribeAsync(subject);
// var t = Task.Run(async () =>
// {
//     int i = 0;
//     await foreach (var msg in sub.Msgs.ReadAllAsync())
//     {
//         if (++i == msgs) break;
//     }
// });
// var bytes = new byte[size];
// for (int j = 0; j < msgs; j++)
// {
//     await nats2.PublishAsync(subject, bytes);
// }
// await t;



await using var nats1 = new NATS.Client.Core.NatsConnection();
await nats1.PingAsync();
await using var nats2 = new NATS.Client.Core.NatsConnection();
await nats2.PingAsync();
var natsKey = new NATS.Client.Core.NatsKey(subject);
var stopwatch = Stopwatch.StartNew();
var sub = await nats1.SubscribeAsync(subject);
var t = Task.Run(async () =>
{
    int i = 0;
    await foreach (var msg in sub.Msgs.ReadAllAsync())
    {
        if (++i == msgs) break;
    }
});
var bytes = new byte[size];
for (int j = 0; j < msgs; j++)
{
    nats2.PostPublish(natsKey, bytes);
}
Console.WriteLine(stopwatch.Elapsed);
await t;


var elapsed = stopwatch.Elapsed;
var totalSeconds = elapsed.TotalSeconds;
Console.WriteLine($"pub/sub stats: {2 * msgs / totalSeconds:n0} msgs/sec ~ {2 * msgs * size / (1024.0 * 1024.0) / totalSeconds:n2} MB/sec");
Console.WriteLine(elapsed);