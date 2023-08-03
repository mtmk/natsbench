
using NATS.Client.Core;

var nats = new NatsConnection();

var js = new NatsJSContext(nats);

var response1 = js.Streams.Add("stream1");
var response2 = js.Streams.Add("stream1");
var msg = response2.Result.GetMsg();

var resonse3 = js.Consumers.Add("stream1", "consumer1");

public class NatsJSContext
{
    public NatsJSContext(NatsConnection nats)
    {
        throw new NotImplementedException();
    }

    public NatsJSStreams Streams { get; set; }
    public NatsJSConsumers Consumers { get; set; }
}

public class NatsJSConsumers
{
    public NatsJSResponse<NatsJSConsumer> Add(string stream1, string consumer1)
    {
        return new NatsJSResponse<NatsJSConsumer> { Result = new NatsJSConsumer() };
    }
}

public class NatsJSConsumer
{
}

public class NatsJSStreams
{
    public NatsJSResponse<NatsJSStream> Add(string name)
    {
        return new NatsJSResponse<NatsJSStream> { Result = new NatsJSStream() };
    }
}

public class NatsJSResponse<T>
{
    public T Result { get; set; }
}

public class NatsJSStream
{
    public NatsMsg GetMsg()
    {
        throw new NotImplementedException();
    }
}