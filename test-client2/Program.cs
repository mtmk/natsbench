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

await using var nats = new NatsConnection(natsOpts);

nats.ConnectionDisconnected += async (_, _) => logger.LogWarning($"[CON] Disconnected");
nats.ConnectionOpened += async (_, _) => logger.LogInformation($"[CON] Connected to {nats.ServerInfo?.Name}");

await nats.ConnectAsync();

var js = new NatsJSContext(nats);

var stream = await js.CreateStreamAsync(
    new StreamConfig("s2", new[] { "s2.*" }) { NumReplicas = 3 },
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
                        var ack = await js.PublishAsync(
                            subject: "s2.x",
                            data: $"data_[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}]_{i:D5}",
                            // opts: new NatsJSPubOpts { MsgId = $"{i:D5}" },
                            cancellationToken: cts.Token);
                        ack.EnsureSuccess();

                        await File.AppendAllTextAsync($"test_publish.txt",
                            $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff} [SND] ({i})\n", cts.Token);

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

                    await Task.Delay(TimeSpan.FromSeconds(.5), cancellationToken);
                }
            }
            catch (NatsJSException e)
            {
                logger.LogError(e, "Publish error");
            }

            await Task.Delay(TimeSpan.FromSeconds(.5), cancellationToken);
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

var consumer = await js.CreateOrUpdateConsumerAsync(
    "s2",
    new ConsumerConfig
    {
        Name = "c2",
        DurableName = "c2",
        AckPolicy = ConsumerConfigAckPolicy.Explicit,
        NumReplicas = 3,
    },
    cancellationToken);

logger.LogInformation("Created consumer {Name}", consumer.Info.Config.Name);

try
{
    var count = 0;
    await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: cts.Token))
    {
        await File.AppendAllTextAsync($"test_consume.txt",
            $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff} [RCV] ({count}) {msg.Subject}: {msg.Data}\n", cts.Token);
        await msg.AckAsync(cancellationToken: cts.Token);
        count++;
    }
}
catch (OperationCanceledException)
{
}

logger.LogInformation("Bye");

await publisher;
