using NATS.Client.Core;

await using var connection = new NatsConnection();
await connection.PingAsync();

Console.ReadLine();

Console.Error.WriteLine("SUB1======================================================");
await connection.SubscribeAsync<byte[]>("foo", m=>
{
    Console.Error.Write("[CALLBACK-1] RCV: ");
    foreach (var b in m)
    {
        Console.Error.Write($"{b:X} ");
    }
    Console.Error.WriteLine();
});

Console.ReadLine();

Console.Error.WriteLine("SUB2======================================================");
await connection.SubscribeAsync<byte[]>("foo", m =>
{
    Console.Error.Write("[CALLBACK-2] RCV: ");
    foreach (var b in m)
    {
        Console.Error.Write($"{b:X} ");
    }
    Console.Error.WriteLine();
}).ConfigureAwait(false);

byte[] array = { 65, 66, 67 };
ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>(array);
NatsKey natsKey = new NatsKey("foo");
for (int i = 0; i < 100; i++)
{
    Console.Error.WriteLine("PUB=============================================");
    await connection.PublishAsync(natsKey, readOnlyMemory);
    Console.ReadLine();
}


for (int i = 0; i < 10; i++)
{
    var timeSpan = await connection.PingAsync();
    Console.WriteLine($"ping:{timeSpan}");
}

for (int i = 0; i < 10; i++)
{
    await connection.PublishAsync("foo", $"bla-{i}").ConfigureAwait(false);
}

await connection.PublishAsync("foo", "bla2").ConfigureAwait(false);

Console.ReadKey();

Console.WriteLine("exit");
