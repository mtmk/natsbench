using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.Core.Internal;

namespace NATS.Client.JetStream.Internal;
internal class NatsJSSubFetch<T> : NatsJSSubBase<T>, INatsJSSubConsume<T>
{
    private readonly Action<NatsJSNotification>? _errorHandler;
    private readonly CancellationToken _cancellationToken;
    private readonly Task _notifier;
    private readonly Channel<NatsJSNotification> _notificationChannel;
    private readonly Channel<NatsJSMsg<T?>> _userMessageChannel;

    internal NatsJSSubFetch(
        string stream,
        string consumer,
        NatsJSContext context,
        ISubscriptionManager manager,
        string subject,
        NatsSubOpts? opts,
        NatsJSSubState state,
        INatsSerializer serializer,
        Action<NatsJSNotification>? errorHandler = default,
        CancellationToken cancellationToken = default)
        : base(stream, consumer, context, manager, subject, opts, state, serializer, cancellationToken)
    {
        _errorHandler = errorHandler;
        _cancellationToken = cancellationToken;

        // User messages are buffered here separately to allow smoother flow while control loop
        // pulls more data in the background. This also allows control messages to be dealt with
        // in the same loop as the control messages to keep state updates consistent. This is as
        // opposed to having a control and a message channel at the point of serializing the messages
        // in NatsJSSub class.
        _userMessageChannel = Channel.CreateBounded<NatsJSMsg<T?>>(NatsSub.GetChannelOptions(opts?.ChannelOptions));

        // We drop the old message if notification handler isn't able to keep up.
        // This is to avoid blocking the control loop and making sure we deliver all the messages.
        // Assuming newer messages would be more relevant and worth keeping than older ones.
        _notificationChannel = Channel.CreateBounded<NatsJSNotification>(new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false,
        });
        _notifier = Task.Run(NotificationLoop);
    }

    public ChannelReader<NatsJSMsg<T?>> Msgs => _userMessageChannel.Reader;

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _notifier;
    }

    protected override void HeartbeatTimerCallback() =>
        _notificationChannel.Writer.WriteAsync(new NatsJSNotification(-1, "Heartbeat timeout"), _cancellationToken);

    protected override ValueTask ReceivedControlMsg(NatsJSNotification notification)
    {
        return _notificationChannel.Writer.WriteAsync(notification, _cancellationToken);
    }

    protected override ValueTask ReceivedUserMsg(NatsMsg<T?> msg)
    {
        return _userMessageChannel.Writer.WriteAsync(new NatsJSMsg<T?>(msg), _cancellationToken);
    }

    protected override void TryComplete()
    {
        _userMessageChannel.Writer.Complete();
        _notificationChannel.Writer.Complete();
    }

    private async Task NotificationLoop()
    {
        await foreach (var notification in _notificationChannel.Reader.ReadAllAsync(_cancellationToken))
        {
            try
            {
                _errorHandler?.Invoke(notification);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "User notification callback error");
            }
        }
    }
}
