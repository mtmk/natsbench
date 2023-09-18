using NATS.Client.Core;
using NATS.Client.JetStream;

// Start the server:
// > nats-server -js

await using var nats = new NatsConnection();
var js = new NatsJSContext(nats);

await js.CreateStreamAsync("orders", subjects: new []{"orders.>"});

for (var i = 0; i < 10; i++)
    await js.PublishAsync($"orders.new.{i}", new Order(i));

var consumer = await js.CreateConsumerAsync(stream: "orders", consumer: "order_processor");

await foreach (var msg in consumer.ConsumeAllAsync<Order>())
{
    var order = msg.Data;
    Console.WriteLine($"Processing {msg.Subject}: {order.OrderId}...");
    await msg.AckAsync();
}

record Order(int OrderId);

