using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

CancellationTokenSource cts1 = new();
var cancellationToken = cts1.Token;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
var logger = loggerFactory.CreateLogger("MAIN");
var natsOpts = NatsOpts.Default with { LoggerFactory = loggerFactory, };

natsOpts = natsOpts with { Url = "127.0.0.1:4441" };

await using var nats1 = new NatsConnection(natsOpts);
await using var nats2 = new NatsConnection(natsOpts);

nats1.ConnectionDisconnected += async (_, _) => logger.LogWarning($"[CON-1] Disconnected");
nats1.ConnectionOpened += async (_, _) => logger.LogInformation($"[CON-1] Connected to {nats1.ServerInfo?.Name}");

nats2.ConnectionDisconnected += async (_, _) => logger.LogWarning($"[CON-2] Disconnected");
nats2.ConnectionOpened += async (_, _) => logger.LogInformation($"[CON-2] Connected to {nats2.ServerInfo?.Name}");

await nats1.ConnectAsync();
await nats2.ConnectAsync();

var js1 = new NatsJSContext(nats1);
var js2 = new NatsJSContext(nats2);

var stream = await js1.CreateStreamAsync(
    new StreamConfig("s1", new[] { "s1.*" }) { NumReplicas = 3 },
    cancellationToken);

logger.LogInformation("Created stream {Name}", stream.Info.Config.Name);

var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

var publisher = Task.Run(async () =>
{
    try
    {
        logger.LogInformation("Starting publishing...");
        for (var i = 0;; i++)
        {
            try
            {
                for (var j = 0; j < 10; j++)
                {
                    try
                    {
                        // var opts = new NatsJSPubOpts { MsgId = $"{i}" };
                        //
                        // if (i > 0)
                        //     opts = opts with { ExpectedLastMsgId = $"{i - 1}" };

                        var ack = await js2.PublishAsync(
                            subject: "s1.x",
                            data: i,
                            //opts: opts,
                            cancellationToken: cts.Token);
                        ack.EnsureSuccess();

                        var message = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff} [SND] ({i})\n";
                        Console.WriteLine(message);
                        await File.AppendAllTextAsync($"test_publish.txt", message, cts.Token);

                        break;
                    }
                    catch (NatsJSDuplicateMessageException)
                    {
                        logger.LogWarning("Publish duplicate. Ignoring...");
                        break;
                    }
                    catch (NatsJSPublishNoResponseException)
                    {
                        logger.LogWarning($"Publish no response. Retrying({j + 1}/10)...");
                    }
                    catch (NatsNoRespondersException)
                    {
                        logger.LogWarning($"Publish no responders. Retrying({j + 1}/10)...");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            catch (NatsJSException e)
            {
                logger.LogError(e, "Publish error");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }
    catch (Exception e)
    {
        logger.LogError(e, "Publish loop error");
    }
    finally
    {
        cts.Cancel();
    }
});

var consumer = await js1.CreateOrderedConsumerAsync("s1", cancellationToken: cancellationToken);

logger.LogInformation("Created ordered consumer");

try
{
    var count = 0;
    await foreach (var msg in consumer.ConsumeAsync<int>(opts: new NatsJSConsumeOpts { MaxMsgs = 100 },
                       cancellationToken: cts.Token))
    {
        if (count != msg.Data)
            throw new Exception($"Unordered {count} != {msg.Data}");

        var message = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff} [RCV] ({count}) {msg.Subject}: {msg.Data}\n";
        Console.WriteLine(message);
        await File.AppendAllTextAsync($"test_consume.txt", message, cts.Token);
        await msg.AckAsync(cancellationToken: cts.Token);
        count++;
    }
}
catch (OperationCanceledException)
{
}

logger.LogInformation("Bye");

await publisher;