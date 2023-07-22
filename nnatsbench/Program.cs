using System.Diagnostics;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using client1;

// using AlterNats;
using NATS.Client.Core;

var subject = "foo";
var msgs = 1_000_000;
var size = 128;
Console.WriteLine($"Starting [subject={subject}, msgs={msgs:n0}, msgsize={size}]");


// var tcpClient = new TcpClient();
// tcpClient.Connect(IPAddress.Loopback, 4222);
// var networkStream = tcpClient.GetStream();
// var writer = new StreamWriter(networkStream, Encoding.ASCII);
// var reader = new StreamReader(networkStream, Encoding.ASCII);
// var stopwatch = Stopwatch.StartNew();
// int i = 0;
// var connected = new ManualResetEventSlim();
// var t = Task.Run(async () =>
// {
//     int msgCount = 0;
//     while (true)
//     {
//         var line = await reader.ReadLineAsync();
//         if (line == null) break;
//         if (line[0] == 'I')
//         {
//             connected.Set();
//             continue;
//         }
//         if (line[0] == 'P')
//         {
//             continue;
//         }
//         if (line[0] == 'M')
//         {
//             msgCount++;
//             await reader.ReadLineAsync();
//             var ii = Interlocked.Increment(ref i);
//             if (ii == msgs) break;
//         }
//     }
// });
// await writer.WriteLineAsync("CONNECT {\"verbose\":false}");
// await writer.FlushAsync();
// connected.Wait();
// await writer.WriteLineAsync($"SUB {subject} 1");
// await writer.FlushAsync();
// var pub = $"PUB {subject} {size}";
// var payload = new string('\0', size);
// for (int j = 0; j < msgs; j++)
// {
//     await writer.WriteLineAsync(pub);
//     await writer.WriteLineAsync(payload);
//     await writer.FlushAsync();
// }
// Console.WriteLine("pub done");
// await t;
// await writer.WriteLineAsync($"UNSUB 1");
// await writer.FlushAsync();



// var nats1 = NatsClient.Connect();
// nats1.Ping();
// var nats2 = NatsClient.Connect();
// nats2.Ping();
// int i = 0;
// var r = new ManualResetEventSlim();
// var stopwatch = Stopwatch.StartNew();
// nats1.Sub(subject, "", m =>
// {
//      var ii = Interlocked.Increment(ref i);
//      if (ii == msgs) r.Set();
// });
// var payload = new string('\0', 128);
// for (int j = 0; j < msgs; j++)
// {
//      nats2.Pub(subject, "", payload);
// }
// r.Wait();



await using var nats1 = new AlterNats.NatsConnection();
await nats1.PingAsync();
await using var nats2 = new AlterNats.NatsConnection();
await nats2.PingAsync();
int i = 0;
var r = new ManualResetEventSlim();
var natsKey = new AlterNats.NatsKey(subject);
var stopwatch = Stopwatch.StartNew();
nats1.SubscribeAsync(natsKey, () =>
{
    var ii = Interlocked.Increment(ref i);
    if (ii == msgs) r.Set();
});
var bytes = new byte[size];
for (int j = 0; j < msgs; j++)
{
    nats2.PostPublish(natsKey, bytes);
}
r.Wait();



// await using var nats1 = new NatsConnection();
// await nats1.PingAsync();
// await using var nats2 = new NatsConnection();
// await nats2.PingAsync();
// Thread.Sleep(1000);
// await using var sub = await nats1.SubscribeAsync(subject);
// var stopwatch = Stopwatch.StartNew();
// var subReader = Task.Run(async () =>
// {
//      var count = 0;
//      await foreach (var msg in sub.Msgs.ReadAllAsync())
//      {
//          if (++count == msgs) break;
//      }
//      await sub.UnsubscribeAsync();
// });
// var payload = new ReadOnlySequence<byte>(new byte[size]);
// for (int i = 0; i < msgs; i++)
// {
//     nats2.PubAsync(subject, payload: payload);
// }
// await subReader;
// await sub.UnsubscribeAsync();


var elapsed = stopwatch.Elapsed;
var totalSeconds = elapsed.TotalSeconds;
Console.WriteLine($"pub/sub stats: {2 * msgs / totalSeconds:n0} msgs/sec ~ {2 * msgs * size / (1024.0 * 1024.0) / totalSeconds:n2} MB/sec");
Console.WriteLine(elapsed);