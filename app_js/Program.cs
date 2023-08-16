using NATS.Client.Core;
using NATS.Client.JetStream;

await using var nats = new NatsConnection();
var js = new NatsJSContext(nats);
var consumer = await js.GetConsumerAsync("s1", "c2");

var tasks = new List<Task>();

// tasks.Add(Task.Run(async () =>
// {
//     var cc = await consumer.ConsumeAsync<int>(new NatsJSConsumeOpts
//     {
//
//     });
//
//     await foreach (var msg in cc.Msgs.ReadAllAsync())
//     {
//         Console.WriteLine($"Consume: {msg.Msg.Subject}: {msg.Msg.Data}");
//         await msg.AckAsync();
//     }
// }));

tasks.Add(Task.Run(async () =>
{
    while (true)
    {
        Console.WriteLine($"Fetching 10...");
        var cf = await consumer.FetchAsync<int>(new NatsJSFetchOpts { MaxMsgs = 10 });
        await foreach (var msg in cf.Msgs.ReadAllAsync())
        {
            Console.WriteLine($"Fetch: {msg.Msg.Subject}: {msg.Msg.Data}");
        }
    }
}));

await Task.WhenAll(tasks);