using NATS.Client.Core;
using NATS.Client.JetStream;

await using var nats = new NatsConnection();
var js = new NatsJSContext(nats);

await js.CreateStreamAsync(stream: "order_stream", subjects: new []{"orders.>"});

for (var i = 0; i < 10; i++)
    await js.PublishAsync($"orders.new.{i}", new Order(Id: 1));

var consumer = await js.CreateConsumerAsync(stream: "order_stream", consumer: "orders_proc");

await foreach (var msg in consumer.ConsumeAllAsync<Order>())
{
    var order = msg.Data;
    Console.WriteLine($"Processing {msg.Subject}: {order.Id}...");
    await msg.AckAsync();
}

record Order(int Id);