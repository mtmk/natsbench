using NATS.Client.Core;

class Program
{
    static async Task Main()
    {
        Console.Error.WriteLine($"Starting..");

        await using var nats = new NatsConnection();
        {
            var res = await nats.RequestAsync<object?, string>("$JS.API.STREAM.INFO.events", null);
            Console.WriteLine(res.Value.Data);
        }
        {
            var res = await nats.RequestAsync<object?, string>("$JS.API.INFO", null);
            Console.WriteLine(res.Value.Data);
        }
    }
}