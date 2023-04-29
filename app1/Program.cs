using NATS.Client.Core;

await using var connection = new NatsConnection();

// Console.WriteLine("f1");
//
// byte[] array = {65,66,67};
// var readOnlyMemory = new ReadOnlyMemory<byte>(array);
//
// NatsKey natsKey = new NatsKey("foo");
//
// await connection.PublishAsync(natsKey, readOnlyMemory);
// Console.WriteLine("f2");


//
// for (int i = 0; i < 100; i++)
// {
//     var timeSpan = await connection.PingAsync();
//     Console.WriteLine(timeSpan);
//     Console.ReadKey();
// }
//

await connection.SubscribeAsync<string>("sub1", s =>
{
    //Console.WriteLine(s);
}).ConfigureAwait(false);

using var natsSubscriber = await connection.CreateSubscriberAsync<string>(new NatsSubscriberOptions
{
    Subject = "sub1"
}).ConfigureAwait(false);

Task.Run(async () =>
{
    await foreach (var message in natsSubscriber.Messages.ReadAllAsync().ConfigureAwait(false))
    {
        Console.WriteLine($"SUB-NEW-{message}");
    }
});

using NatsPublisher<string> natsPublisher = await connection.CreatePublisherAsync<string>(new NatsPublisherOptions
{
    Subject = "sub1"
}).ConfigureAwait(false);
await natsPublisher.PublishAsync("123").ConfigureAwait(false);

for (int i = 0; i < 100; i++)
{
    await connection.PublishAsync("sub1", $"bla-{i}").ConfigureAwait(false);
    await connection.PublishAsync(new NatsPubMsg<string>
    {
        Data = $"NEW-bla-{i}",
        Subject = "sub1",
    }).ConfigureAwait(false);
    await natsPublisher.PublishAsync($"NEW-NEW-blah123-{i}").ConfigureAwait(false);
    Console.ReadKey();
}

//
// Console.WriteLine("f3");
// await connection.PublishAsync("foo", "bla2").ConfigureAwait(false);
// Console.WriteLine("f4");

Console.ReadKey();

Console.WriteLine("exit");
