using System;
using System.Collections.Generic;

Span<int> a = new []{1, 2, 3};

new HashSet<int>();

//
// using System;
// using Microsoft.Extensions.Logging;
// using NATS.Client.Core;
// using NATS.Client.JetStream;
//
// var opts = NatsOpts.Default with { LoggerFactory = new MinimumConsoleLoggerFactory(LogLevel.Error) };
//
// await using var nc = new NatsConnection(opts);
// var js = new NatsJSContext(nc);
//
// await js.CreateStreamAsync("orders", subjects: new []{"orders.*"});
//
// for (var i = 0; i < 10; i++)
// {
//     var ack = await js.PublishAsync($"orders.{i}", new Order(i));
//     ack.EnsureSuccess();
// }
//
// var consumer = await js.CreateConsumerAsync("orders", "order_processor");
//
// Console.WriteLine($"Consume...");
// await foreach (var msg in consumer.ConsumeAllAsync<Order>(new NatsJSConsumeOpts { MaxMsgs = 10 }))
// {
//     var order = msg.Data;
//     Console.WriteLine($"Processing order {order.OrderId}...");
//     await msg.AckAsync();
//     if (order.OrderId == 5)
//         break;
// }
// Console.WriteLine($"Done consuming.");
//
// Console.WriteLine($"Fetch...");
// await foreach (var msg in consumer.FetchAllAsync<Order>(new NatsJSFetchOpts { MaxMsgs = 10 }))
// {
//     var order = msg.Data;
//     Console.WriteLine($"Processing order {order.OrderId}...");
//     await msg.AckAsync();
// }
// Console.WriteLine($"Done fetching.");
//
// record Order(int OrderId);
//
