using System.Buffers;
using System.Diagnostics;
using NATS.Client.Core;

var subject = args[0];
var msgs = int.Parse(args[1]);
var size = 128;

Console.WriteLine($"Starting [subject={subject}, msgs={msgs:n0}, msgsize={size}]");
var stopwatch = Stopwatch.StartNew();

await using var nats1 = new NatsConnection();
await using var sub = await nats1.SubscribeAsync(subject);

var t = Task.Run(async () =>
{
   var count = 0;
   await foreach (var msg in sub.Msgs.ReadAllAsync())
   {
      if (++count == msgs)
      {
         break;
      }
   }
   
   await sub.UnsubscribeAsync();
});

await using var nats2 = new NatsConnection();
var payload = new ReadOnlySequence<byte>(new byte[size]);
for (int i = 0; i < msgs; i++)
{
   // await
   nats2.PublishAsync(subject, payload);
}

await t;

var totalSeconds = stopwatch.Elapsed.TotalSeconds;

Console.WriteLine(
   $"pub/sub stats: {2 * msgs / totalSeconds:n0} msgs/sec ~ {2 * msgs * size / (1024.0 * 1024.0) / totalSeconds:n2} MB/sec");
