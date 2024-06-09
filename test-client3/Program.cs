using NATS.Client.Core;
using NATS.Client.Services;

var builder = WebApplication.CreateBuilder();

await using var nats1 = new NatsConnection(new NatsOpts { Url = "127.0.0.1:4441" });
await using var nats2 = new NatsConnection(new NatsOpts { Url = "127.0.0.1:4442" });
await using var nats3 = new NatsConnection(new NatsOpts { Url = "127.0.0.1:4443" });

foreach (var nats in new[] { nats1, nats2, nats3 })
{
    while (true)
    {
        try
        {
            await nats.ConnectAsync();
            break;
        }
        catch
        {
            Console.WriteLine("Retrying initial connection...");
        }
    }
}

builder.Services.AddKeyedSingleton("nats1", nats1);
builder.Services.AddKeyedSingleton("nats2", nats2);
builder.Services.AddKeyedSingleton("nats3", nats3);
builder.Services.AddHostedService<Service1>();
builder.Services.AddHostedService<Service2>();

await using var app = builder.Build();

await app.RunAsync();

class Service1(
    [FromKeyedServices("nats1")] NatsConnection nats1,
    [FromKeyedServices("nats2")] NatsConnection nats2,
    [FromKeyedServices("nats3")] NatsConnection nats3) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var nats in new[] { nats1, nats2, nats3 })
        {
            var context = new NatsSvcContext(nats);
            var s1 = await context.AddServiceAsync("s1", "1.0.0");
            await s1.AddEndpointAsync<string>(async m =>
            {
                GC.Collect();

                if (m.Exception is { } exception)
                {
                    Console.WriteLine($"[ERR] {exception.Message}");
                    return;
                }

                await m.ReplyAsync($"you said '{m.Data}'");
            }, "e1");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

class Service2(
    [FromKeyedServices("nats1")] NatsConnection nats1,
    [FromKeyedServices("nats2")] NatsConnection nats2,
    [FromKeyedServices("nats3")] NatsConnection nats3) : IHostedService
{
    private List<Task> _tasks = new();
    private CancellationTokenSource _cts;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        foreach (var action in new[] { "INFO", "PING", "STATS" })
        {
            _tasks.Add(Task.Run(async () =>
            {
                while (_cts.IsCancellationRequested == false)
                {
                    try
                    {
                        var reply = await await Task.WhenAny([
                            nats1.RequestAsync<string>($"$SRV.{action}", cancellationToken: _cts.Token).AsTask(),
                            nats2.RequestAsync<string>($"$SRV.{action}", cancellationToken: _cts.Token).AsTask(),
                            nats3.RequestAsync<string>($"$SRV.{action}", cancellationToken: _cts.Token).AsTask(),
                        ]);
                        Console.WriteLine($"[{action}] {reply.Data}");
                        await Task.Delay(3000, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERR] {action}: {ex.Message}");
                    }
                }
            }));
        }

        _tasks.Add(Task.Run(async () =>
        {
            while (_cts.IsCancellationRequested == false)
            {
                try
                {
                    var reply = await await Task.WhenAny([
                        nats1.RequestAsync<string, string>("e1", "x", cancellationToken: _cts.Token).AsTask(),
                        nats2.RequestAsync<string, string>("e1", "x", cancellationToken: _cts.Token).AsTask(),
                        nats3.RequestAsync<string, string>("e1", "x", cancellationToken: _cts.Token).AsTask(),
                    ]);
                    Console.WriteLine($"[CALL] {reply.Data}");
                    await Task.Delay(3000, _cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERR] CALL: {ex.Message}");
                }
            }
        }));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        foreach (var task in _tasks)
        {
            try
            {
                await task;
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}