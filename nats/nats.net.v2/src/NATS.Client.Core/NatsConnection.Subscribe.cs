using System.Collections.Concurrent;
using NATS.Client.Core.Commands;
using NATS.Client.Core.Internal;

namespace NATS.Client.Core;

public partial class NatsConnection
{
    /// <inheritdoc />
    public ValueTask<NatsSub> SubscribeAsync(NatsSubject subject, in NatsSubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        return SubAsync<NatsSub>(subject, opts, NatsSubBuilder.Default, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<NatsSub<T>> SubscribeAsync<T>(NatsSubject subject, in NatsSubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        var serializer = opts?.Serializer ?? Options.Serializer;
        return SubAsync<NatsSub<T>>(subject, opts, NatsSubModelBuilder<T>.For(serializer), cancellationToken);
    }
}
