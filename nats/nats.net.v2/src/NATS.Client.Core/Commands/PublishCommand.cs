using System.Buffers;
using System.Text;
using NATS.Client.Core.Internal;

namespace NATS.Client.Core.Commands;

public record struct NatsSubject
{
    private readonly byte[] _subject;

    public NatsSubject(byte[] subject)
    {
        _subject = subject;
    }

    public NatsSubject(string subject)
    {
        _subject = Encoding.ASCII.GetBytes(subject);
    }

    public ReadOnlySpan<byte> AsSpan() => _subject.AsSpan();

    public int Length => _subject.Length;

    public bool StartsWith(string inboxPrefix)
    {
        return AsSpan().StartsWith(Encoding.ASCII.GetBytes(inboxPrefix));
    }
}
internal sealed class PublishCommand<T> : CommandBase<PublishCommand<T>>
{
    private NatsSubject? _subject;
    private NatsSubject? _replyTo;
    private NatsHeaders? _headers;
    private T? _value;
    private INatsSerializer? _serializer;
    private CancellationToken _cancellationToken;

    private PublishCommand()
    {
    }

    public override bool IsCanceled => _cancellationToken.IsCancellationRequested;

    public static PublishCommand<T> Create(ObjectPool pool, NatsSubject subject, NatsSubject? replyTo, NatsHeaders? headers, T? value, INatsSerializer serializer, CancellationToken cancellationToken)
    {
        if (!TryRent(pool, out var result))
        {
            result = new PublishCommand<T>();
        }

        result._subject = subject;
        result._replyTo = replyTo;
        result._headers = headers;
        result._value = value;
        result._serializer = serializer;
        result._cancellationToken = cancellationToken;

        return result;
    }

    public override void Write(ProtocolWriter writer)
    {
        writer.WritePublish(_subject!.Value, _replyTo, _headers, _value, _serializer!);
    }

    protected override void Reset()
    {
        _subject = default;
        _headers = default;
        _value = default;
        _serializer = null;
        _cancellationToken = default;
    }
}

internal sealed class PublishBytesCommand : CommandBase<PublishBytesCommand>
{
    private NatsSubject? _subject;
    private NatsSubject? _replyTo;
    private NatsHeaders? _headers;
    private ReadOnlySequence<byte> _payload;
    private CancellationToken _cancellationToken;

    private PublishBytesCommand()
    {
    }

    public override bool IsCanceled => _cancellationToken.IsCancellationRequested;

    public static PublishBytesCommand Create(ObjectPool pool, NatsSubject subject, NatsSubject? replyTo, NatsHeaders? headers, ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
    {
        if (!TryRent(pool, out var result))
        {
            result = new PublishBytesCommand();
        }

        result._subject = subject;
        result._replyTo = replyTo;
        result._headers = headers;
        result._payload = payload;
        result._cancellationToken = cancellationToken;

        return result;
    }

    public override void Write(ProtocolWriter writer)
    {
        writer.WritePublish(_subject!.Value, _replyTo, _headers, _payload);
    }

    protected override void Reset()
    {
        _subject = default;
        _replyTo = default;
        _headers = default;
        _payload = default;
        _cancellationToken = default;
    }
}

internal sealed class AsyncPublishCommand<T> : AsyncCommandBase<AsyncPublishCommand<T>>
{
    private NatsSubject? _subject;
    private NatsSubject? _replyTo;
    private NatsHeaders? _headers;
    private T? _value;
    private INatsSerializer? _serializer;

    private AsyncPublishCommand()
    {
    }

    public static AsyncPublishCommand<T> Create(ObjectPool pool, CancellationTimer timer, NatsSubject subject, NatsSubject? replyTo, NatsHeaders? headers, T? value, INatsSerializer serializer)
    {
        if (!TryRent(pool, out var result))
        {
            result = new AsyncPublishCommand<T>();
        }

        result._subject = subject;
        result._replyTo = replyTo;
        result._headers = headers;
        result._value = value;
        result._serializer = serializer;
        result.SetCancellationTimer(timer);

        return result;
    }

    public override void Write(ProtocolWriter writer)
    {
        writer.WritePublish(_subject!.Value, _replyTo, _headers, _value, _serializer!);
    }

    protected override void Reset()
    {
        _subject = default;
        _headers = default;
        _value = default;
        _serializer = null;
    }
}

internal sealed class AsyncPublishBytesCommand : AsyncCommandBase<AsyncPublishBytesCommand>
{
    private NatsSubject? _subject;
    private NatsSubject? _replyTo;
    private NatsHeaders? _headers;
    private ReadOnlySequence<byte> _payload;

    private AsyncPublishBytesCommand()
    {
    }

    public static AsyncPublishBytesCommand Create(ObjectPool pool, CancellationTimer timer, NatsSubject subject, NatsSubject? replyTo, NatsHeaders? headers, ReadOnlySequence<byte> payload)
    {
        if (!TryRent(pool, out var result))
        {
            result = new AsyncPublishBytesCommand();
        }

        result._subject = subject;
        result._replyTo = replyTo;
        result._headers = headers;
        result._payload = payload;
        result.SetCancellationTimer(timer);

        return result;
    }

    public override void Write(ProtocolWriter writer)
    {
        writer.WritePublish(_subject!.Value, _replyTo, _headers, _payload);
    }

    protected override void Reset()
    {
        _subject = default;
        _replyTo = default;
        _headers = default;
        _payload = default;
    }
}
