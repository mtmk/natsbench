// See https://aka.ms/new-console-template for more information

using NATS.Client.Core;
using NATS.Net;

Console.WriteLine("Hello, World!");

var opts = new NatsOpts { AuthOpts = new NatsAuthOpts { Username = "xlet-me-in" } };
await using var client = new NatsClient(opts);
var rtt = await client.PingAsync();
Console.WriteLine($"RTT: {rtt} ms");
