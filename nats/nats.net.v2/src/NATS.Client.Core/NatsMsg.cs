using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NATS.Client.Core.Commands;

namespace NATS.Client.Core;

public readonly record struct NatsMsg(
    NatsSubject Subject,
    NatsSubject? ReplyTo,
    int Size,
    NatsHeaders? Headers,
    ReadOnlyMemory<byte> Data,
    NatsConnection? Connection)
{
    internal static NatsMsg Build(
        NatsSubject subject,
        NatsSubject? replyTo,
        in ReadOnlySequence<byte>? headersBuffer,
        in ReadOnlySequence<byte> payloadBuffer,
        NatsConnection? connection,
        HeaderParser headerParser)
    {
        NatsHeaders? headers = null;

        if (headersBuffer != null)
        {
            headers = new NatsHeaders();
            if (!headerParser.ParseHeaders(new SequenceReader<byte>(headersBuffer.Value), headers))
            {
                throw new NatsException("Error parsing headers");
            }

            headers.SetReadOnly();
        }

        var size = subject.Length
                   + replyTo?.Length ?? 0
                   + headersBuffer?.Length ?? 0
                   + payloadBuffer.Length;

        return new NatsMsg(subject, replyTo, (int)size, headers, payloadBuffer.ToArray(), connection);
    }

    public ValueTask ReplyAsync(ReadOnlySequence<byte> payload = default, in NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        CheckReplyPreconditions();
        return Connection.PublishAsync(ReplyTo!.Value, payload, opts, cancellationToken);
    }

    public ValueTask ReplyAsync(NatsMsg msg, in NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        CheckReplyPreconditions();
        return Connection.PublishAsync(msg with { Subject = ReplyTo!.Value }, opts, cancellationToken);
    }

    [MemberNotNull(nameof(Connection))]
    private void CheckReplyPreconditions()
    {
        if (Connection == default)
        {
            throw new NatsException("unable to send reply; message did not originate from a subscription");
        }

        if (ReplyTo!.Value.Length == 0)
        {
            throw new NatsException("unable to send reply; ReplyTo is empty");
        }
    }
}

public readonly record struct NatsMsg<T>(
    NatsSubject Subject,
    NatsSubject? ReplyTo,
    int Size,
    NatsHeaders? Headers,
    T? Data,
    NatsConnection? Connection)
{
    internal static NatsMsg<T> Build(
        NatsSubject subject,
        NatsSubject? replyTo,
        in ReadOnlySequence<byte>? headersBuffer,
        in ReadOnlySequence<byte> payloadBuffer,
        NatsConnection? connection,
        HeaderParser headerParser,
        INatsSerializer serializer)
    {
        // Consider an empty payload as null or default value for value types. This way we are able to
        // receive sentinels as nulls or default values. This might cause an issue with where we are not
        // able to differentiate between an empty sentinel and actual default value of a struct e.g. 0 (zero).
        var data = payloadBuffer.Length > 0
            ? serializer.Deserialize<T>(payloadBuffer)
            : default;

        NatsHeaders? headers = null;

        if (headersBuffer != null)
        {
            headers = new NatsHeaders();
            if (!headerParser.ParseHeaders(new SequenceReader<byte>(headersBuffer.Value), headers))
            {
                throw new NatsException("Error parsing headers");
            }

            headers.SetReadOnly();
        }

        var size = subject.Length
            + replyTo?.Length ?? 0
            + headersBuffer?.Length ?? 0
            + payloadBuffer.Length;

        return new NatsMsg<T>(subject, replyTo, (int)size, headers, data, connection);
    }

    public ValueTask ReplyAsync<TReply>(TReply data, in NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        CheckReplyPreconditions();
        return Connection.PublishAsync(ReplyTo!.Value, data, opts, cancellationToken);
    }

    public ValueTask ReplyAsync<TReply>(NatsMsg<TReply> msg)
    {
        CheckReplyPreconditions();
        return Connection.PublishAsync(msg with { Subject = ReplyTo!.Value });
    }

    public ValueTask ReplyAsync(ReadOnlySequence<byte> payload = default, in NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        CheckReplyPreconditions();
        return Connection.PublishAsync(ReplyTo!.Value, payload: payload, opts, cancellationToken);
    }

    public ValueTask ReplyAsync(NatsMsg msg)
    {
        CheckReplyPreconditions();
        return Connection.PublishAsync(msg with { Subject = ReplyTo!.Value });
    }

    [MemberNotNull(nameof(Connection))]
    private void CheckReplyPreconditions()
    {
        if (Connection == default)
        {
            throw new NatsException("unable to send reply; message did not originate from a subscription");
        }

        if (ReplyTo!.Value.Length == 0)
        {
            throw new NatsException("unable to send reply; ReplyTo is empty");
        }
    }
}
