using System.Security.Principal;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;











/****************************************************************************************************************/
//
//  J E T S T R E A M   C O N T E X T
//

await using var nats = new NatsConnection();

var js = new NatsJSContext(nats);


/****************************************************************************************************************/





















/****************************************************************************************************************/
//
//  A C C O U N T   I N F O 
//

var account = await js.GetAccountInfoAsync();

Console.WriteLine($$"""
                    Account
                    Domain: {{account.Domain}}
                    Consumers: {{account.Consumers}}
                    Streams: {{account.Streams}}
                    Api.Errors: {{account.Api.Errors}}
                    Api.Total: {{account.Api.Total}}
                    Limits.MaxConsumers: {{account.Limits.MaxConsumers}}
                    Limits.MaxStreams: {{account.Limits.MaxStreams}}
                    Limits.MaxStorage: {{account.Limits.MaxStorage}}
                    """);



/****************************************************************************************************************/








/****************************************************************************************************************/
//
//  S T R E A M S
//

var stream1 = await js.CreateStreamAsync("stream1", subjects: new[] { "stream1.*" });
stream1 = await js.CreateStreamAsync(new StreamConfiguration
{
    Name = "stream1",
    Subjects = new[] { "stream1.*" },
    Retention = StreamConfigurationRetention.workqueue,
});

stream1 = await js.GetStreamAsync("stream1");

Console.WriteLine($$"""
                    Stream Info:
                    Name: {{ stream1.Info.Config.Name }}
                    Subjects: {{ string.Join(",", stream1.Info.Config.Subjects) }}
                    Created: {{ stream1.Info.Created }}
                    """);

await foreach (var stream in js.ListStreamsAsync(new StreamListRequest { Subject = "stream1.*" }))
    Console.WriteLine($"Stream: {stream.Info.Config.Name}");

stream1 = await js.UpdateStreamAsync(new StreamUpdateRequest { Name = "stream1", MaxMsgs = 1_000_000 });

var isStreamDeleted = await stream1.DeleteAsync();

/****************************************************************************************************************/















/****************************************************************************************************************/
//
//  C O N S U M E R S
//

var consumer1 = await js.CreateConsumerAsync("stream1", "consumer1");
consumer1 = await js.CreateConsumerAsync(new ConsumerCreateRequest
{
    StreamName = "stream1",
    Config = new ConsumerConfiguration
    {
        AckPolicy = ConsumerConfigurationAckPolicy.@explicit,
        DurableName = "consumer1",
        Name = "consumer1",
    },
});

await foreach (var consumer in js.ListConsumersAsync("stream1", new ConsumerListRequest { Offset = 0 }))
{
    Console.WriteLine($"Consumer: {consumer.Info.Name}");
}

consumer1 = await js.GetConsumerAsync("stream1", "consumer1");

Console.WriteLine($$"""
                  Consumer:
                  Name: {{ consumer1.Info.Name }}
                  Stream: {{ consumer1.Info.StreamName }}
                  Created: {{ consumer1.Info.Created }}
                  """);

void ConsumerErrorHandler(NatsJSNotification notification)
{
    Console.WriteLine($"Error: {notification.Code} {notification.Description}");
}

{ // NEXT
    var next = await consumer1.NextAsync<TestData>(new NatsJSNextOpts
    {
        ErrorHandler = ConsumerErrorHandler,
        Expires = TimeSpan.FromSeconds(30),
    });

    if (next is { } msg)
    {
        Console.WriteLine($"{msg.Subject}: {msg.Data.Id}");
    }
}

{ // FETCH
    var fetch = await consumer1.FetchAsync<TestData>(new NatsJSFetchOpts
    {
        MaxMsgs = 100,
        ErrorHandler = ConsumerErrorHandler,
    });
    await foreach (var msg in fetch.Msgs.ReadAllAsync())
    {
        Console.WriteLine($"{msg.Subject}: {msg.Data.Id}");
    }
}

{ // DELETE
    var isConsumerdeleted = await consumer1.DeleteAsync();
}







/****************************************************************************************************************/
//
//  P U B L I S H
//

var ack = await js.PublishAsync("stream1.foo", new TestData { Id = 1 });

Console.WriteLine($$"""
                    ACK:
                    Domain: {{ ack.Domain }}
                    Stream: {{ ack.Stream }}
                    Duplicate: {{ ack.Duplicate }}
                    Seq: {{ ack.Seq }}
                    API Error: {{ ack.Error }}
                    """);

ack.EnsureSuccess();

public class TestData
{
    public int Id { get; set; }
}

/****************************************************************************************************************/



