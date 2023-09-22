using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;




/*
 
 
 
 
 
 
 
                     J e t S t r e a m   . N E T   V 2   p r e v i e w  1

    * Quick review of the API

    * Missing features

    * Questions and Suggestions
 

            
                                                                                             
                                                                             






                                                                                               
*/








/*******************************************************************************************/
//
//  J E T S T R E A M   C O N T E X T
//

var nc = new NatsConnection(NatsOpts.Default with
{
    WaitUntilSent = false,
});

var js = new NatsJSContext(nc, new NatsJSOpts(nc.Opts)
{
    // TODO: Remove MaxMsgs = 1000, // maybe confusing
    
    // TODO: Add domain option
    
    // timeout comes from connection
    
    ApiPrefix = "$JS.API",
    InboxPrefix = "_INBOX",// Remove
    
    
    AckOpts = new AckOpts
    {
        WaitUntilSent = false,
    },
});









/*******************************************************************************************/





















/*******************************************************************************************/
//
//  A C C O U N T   I N F O 
//

var account = await js.GetAccountInfoAsync();

Console.WriteLine($$"""
                    Account
                        Domain: {{ account.Domain }}
                        Consumers: {{ account.Consumers }}
                        Streams: {{ account.Streams }}
                        Api.Errors: {{ account.Api.Errors }}
                        Api.Total: {{ account.Api.Total }}
                        Limits.MaxConsumers: {{ account.Limits.MaxConsumers }}
                        Limits.MaxStreams: {{ account.Limits.MaxStreams }}
                        Limits.MaxStorage: {{ account.Limits.MaxStorage }}
                    """);



/*******************************************************************************************/








/*******************************************************************************************/
//
//  S T R E A M S
//

// * Create Stream
// * Delete Stream
// * Get Stream
// * List Streams
// * Update Stream
    















// CREATE STREAM

var stream1 = await js.CreateStreamAsync("stream1", subjects: new[] { "stream1.*" });

stream1 = await js.CreateStreamAsync(new StreamConfiguration
{
    Name = "stream1",
    Subjects = new[] { "stream1.*" },
    Retention = StreamConfigurationRetention.workqueue,
});





















 // GET STREAM

stream1 = await js.GetStreamAsync("stream1");

Console.WriteLine($$"""
                    Stream Info:
                        Name: {{ stream1.Info.Config.Name }}
                        Subjects: {{ string.Join(",", stream1.Info.Config.Subjects) }}
                        Created: {{ stream1.Info.Created }}
                    """);











// LIST STREAMS
{
    await foreach (var stream in js.ListStreamsAsync())
    {
        Console.WriteLine($"Stream: {stream.Info.Config.Name}");
    }

    // Stream names API (later)
    // Paging implementation (later)
}











// UPDATE STREAM

stream1 = await js.UpdateStreamAsync(new StreamUpdateRequest
{
    Name = "stream1",
    MaxMsgs = 1_000_000,
    
});

Console.WriteLine($"New stream max msgs: {stream1.Info.Config.MaxMsgs}");
















// DELETE STREAM

var isStreamDeleted = await js.DeleteStreamAsync("stream1");

if (!isStreamDeleted)
    Console.WriteLine($"Error deleting stream {stream1.Info.Config.Name}");









/*******************************************************************************************/
// Stream Interface
//
// * stream.Info;
//
// * stream.DeleteAsync();
// * stream.UpdateAsync();
//
// * stream.CreateConsumerAsync();
// * stream.DeleteConsumerAsync();
// * stream.GetConsumerAsync();
// * stream.ListConsumersAsync();




/*******************************************************************************************/















/*******************************************************************************************/
//
//  C O N S U M E R S
//

// * Create Consumer
// * Get Consumer
// * List Consumers
// * Delete Consumer













// CREATE CONSUMER

var consumer1 = await js.CreateConsumerAsync(stream: "stream1", consumer: "consumer1");

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


























// LIST CONSUMERS
{
    await foreach (var consumer in js.ListConsumersAsync("stream1"))
    {
        Console.WriteLine($"Consumer: {consumer.Info.Name}");
    }

    // Names
    // Paging
}










// GET CONSUMER

consumer1 = await js.GetConsumerAsync("stream1", "consumer1");

Console.WriteLine($$"""
                  Consumer:
                      Name: {{ consumer1.Info.Name }}
                      Stream: {{ consumer1.Info.StreamName }}
                      Created: {{ consumer1.Info.Created }}
                  """);












{ // DELETE
    
    var isConsumerDeleted = await consumer1.DeleteAsync();
    
    if (!isConsumerDeleted)
        Console.WriteLine("Error deleting consumer");
    
}












/*******************************************************************************************/
// Consumer Interface
//
// * consumer.Info;
// * consumer.DeleteAsync();
//
//
// Consuming Messages:
//
// * consumer.NextAsync<>();
//
// * consumer.FetchAsync<>();
// * consumer.FetchAllAsync<>();
//
// * consumer.ConsumeAsync<>();
// * consumer.ConsumeAllAsync<>();
//









{ // NEXT

    var opts = new NatsJSNextOpts
    {
        Expires = TimeSpan.FromSeconds(30),
    };
    
    var next = await consumer1.NextAsync<Order>(opts);

    if (next is { } msg)
    {
        Console.WriteLine($"{msg.Subject}: {msg.Data.OrderId}");
        await msg.AckAsync();
        
        // or
        // await msg.NackAsync();
        // await msg.AckProgressAsync();
        // await msg.AckTerminateAsync();
        
        // TODO: Nack with delay
    }
    
    void ErrorHandler(INatsJSFetch _, NatsJSNotification notification)
    {
        Console.WriteLine($"Error: {notification.Code} {notification.Description}");
    }
}













{ // FETCH
    
    var opts = new NatsJSFetchOpts
    {
        MaxMsgs = 100,
        // MaxBytes = 1024, // Either bytes or msgs, throw exception otherwise
        Expires = TimeSpan.FromMinutes(1),
        IdleHeartbeat = TimeSpan.FromSeconds(10),
        // TODO: Remove ErrorHandler = ErrorHandler,
    };

    var fetch = await consumer1.FetchAsync<Order>(opts);

    try
    {
        await foreach (var msg in fetch.Msgs.ReadAllAsync())
        {
            Console.WriteLine($"{msg.Subject}: {msg.Data.OrderId}");
            await msg.AckAsync(new AckOpts
            {
                WaitUntilSent = true,
                // DoubleAck = true
            });
        }
    }
    catch (NatsJSProtocolException e)
    {
        Console.WriteLine(e.Message);
    }
    
    // TODO: 
    void ErrorHandler(INatsJSFetch consumer, NatsJSNotification notification)
    {
        Console.WriteLine($"Error: {notification.Code} {notification.Description}");
        consumer.Stop();
    }
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    // Alternative fetch all
    
    await foreach (var msg in consumer1.FetchAllAsync<Order>(new NatsJSFetchOpts{MaxMsgs=32}))
    {
        Console.WriteLine($"{msg.Subject}: {msg.Data.OrderId}");
        await msg.AckAsync();
    }
    
    
}










{ // CONSUME
    
    var opts = new NatsJSConsumeOpts
    {
        MaxBytes = 1024,
        ThresholdBytes = 256, // default is half of max
        // MaxMsgs = 100, // only allow msgs or bytes, throw exception otherwise
        // ThresholdMsgs = 50,
        Expires = TimeSpan.FromMinutes(2),
        IdleHeartbeat = TimeSpan.FromSeconds(10),
    };
    
    var consume = await consumer1.ConsumeAsync<Order>(opts);

    await foreach (var msg in consume.Msgs.ReadAllAsync())
    {
        Console.WriteLine($"{msg.Subject}: {msg.Data.OrderId}");
        await msg.AckAsync();
    }


    // TODO: terminal state handling
    // TODO: Notification enum
    void ErrorHandler(INatsJSConsume consumer, NatsJSNotification notification)
    {
        Console.WriteLine($"Error: {notification.Code} {notification.Description}");
        consumer.Stop();
    }
    
    
    
    
    
    
    
    
    
    
    
    
    
    // Alternative consume all using asynchronous enumerable
    
    await foreach (var msg in consumer1.ConsumeAllAsync<Order>(new NatsJSConsumeOpts{MaxMsgs=64}))
    {
        Console.WriteLine($"{msg.Subject}: {msg.Data.OrderId}");
        await msg.AckAsync();
    }
    
    
}


















/*******************************************************************************************/
//
//  P U B L I S H
//

var ack = await js.PublishAsync("stream1.foo", new Order(OrderId: 1));

// TODO: message id / js pub opts
ack = await js.PublishAsync("stream1.foo", new Order(OrderId: 1), new NatsPubOpts
{
    Headers = new NatsHeaders { { "Nats-Msg-Id", "2" } },
});

Console.WriteLine($$"""
                    ACK:
                        Domain: {{ ack.Domain }}
                        Stream: {{ ack.Stream }}
                        Duplicate: {{ ack.Duplicate }}
                        Seq: {{ ack.Seq }}
                        API Error: {{ ack.Error }}
                    """);

ack.EnsureSuccess();










/*******************************************************************************************/










/*******************************************************************************************/
// FEATURES NOT IMPLEMENTED
//
// * Stream getting/deleting messages
//     * getMsg(getMsgOpts)
//     * deleteMsg(deleteMsgOpts)
//     * purge(purgeOpts)
//
// * Consume reconnect implemented with following missing
//     * Pause/resume heartbeat timer
//     * Check if consumer exists
//
// * Consume drain()
//
// * Handling leader change
//
// * Double ACK
//     * Publish (?)
//     * Consume (?)



// Questions and Suggestions ?







public record Order(int OrderId);
