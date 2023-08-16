using NATS.Client.Core;
using NATS.Client.JetStream;

await using var nats = new NatsConnection();
var js = new NatsJSContext(nats);

var consumer = await js.GetConsumerAsync("s1", "c2");

var cc = await consumer.ConsumeAsync<int>(new NatsJSConsumeOpts
{
    
});

await foreach (var msg in cc.Msgs.ReadAllAsync())
{
    Console.WriteLine($"{msg.Msg.Subject}: {msg.Msg.Data}");
    await msg.Ack();
}
