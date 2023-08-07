using System.Buffers;
using System.Collections.Concurrent;
using NATS.Client.Core.Commands;

namespace NATS.Client.Core.Internal;

internal interface INatsSub : IAsyncDisposable
{
    NatsSubject Subject { get; }

    string? QueueGroup { get; }

    int? PendingMsgs { get; }

    /// <summary>
    /// Called after subscription is sent to the server.
    /// Helps maintain more accurate timeouts, especially idle timeout.
    /// </summary>
    void Ready();

    ValueTask ReceiveAsync(NatsSubject subject, NatsSubject? replyTo, ReadOnlySequence<byte>? headersBuffer, ReadOnlySequence<byte> payloadBuffer);
}

internal interface INatsSubBuilder<out T>
    where T : INatsSub
{
    T Build(NatsSubject subject, NatsSubOpts? opts, NatsConnection connection, ISubscriptionManager manager);
}

internal class NatsSubBuilder : INatsSubBuilder<NatsSub>
{
    public static readonly NatsSubBuilder Default = new();

    public NatsSub Build(NatsSubject subject, NatsSubOpts? opts, NatsConnection connection, ISubscriptionManager manager)
    {
        return new NatsSub(connection, manager, subject, opts);
    }
}

internal class NatsSubModelBuilder<T> : INatsSubBuilder<NatsSub<T>>
{
    private static readonly ConcurrentDictionary<INatsSerializer, NatsSubModelBuilder<T>> Cache = new();
    private readonly INatsSerializer _serializer;

    public NatsSubModelBuilder(INatsSerializer serializer) => _serializer = serializer;

    public static NatsSubModelBuilder<T> For(INatsSerializer serializer) =>
        Cache.GetOrAdd(serializer, static s => new NatsSubModelBuilder<T>(s));

    public NatsSub<T> Build(NatsSubject subject, NatsSubOpts? opts, NatsConnection connection, ISubscriptionManager manager)
    {
        return new NatsSub<T>(connection, manager, subject, opts, _serializer);
    }
}
