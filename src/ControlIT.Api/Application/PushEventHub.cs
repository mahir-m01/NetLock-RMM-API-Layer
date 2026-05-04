namespace ControlIT.Api.Application;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlIT.Api.Domain.DTOs.Responses;

public readonly record struct PushSubscriptionScope(bool IsAllTenants, int? TenantId)
{
    public static PushSubscriptionScope From(TenantContext tenant) =>
        new(tenant.IsAllTenants, tenant.TenantId);
}

public interface IPushEventPublisher
{
    ValueTask PublishAsync(PushEventEnvelope evt, CancellationToken ct = default);

    IAsyncEnumerable<PushEventEnvelope> SubscribeAsync(
        PushSubscriptionScope scope,
        CancellationToken ct = default);
}

public sealed class PushEventHub : IPushEventPublisher
{
    private const int ChannelCapacity = 2048;

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();

    public ValueTask PublishAsync(PushEventEnvelope evt, CancellationToken ct = default)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            if (CanReceive(subscriber.Scope, evt))
                subscriber.Channel.Writer.TryWrite(evt);
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<PushEventEnvelope> SubscribeAsync(
        PushSubscriptionScope scope,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<PushEventEnvelope>(new BoundedChannelOptions(ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _subscribers[id] = new Subscriber(scope, channel);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
                yield return evt;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }

    public static bool CanReceive(PushSubscriptionScope scope, PushEventEnvelope evt)
    {
        if (evt.TenantId is null)
            return true;

        if (scope.IsAllTenants)
            return true;

        return scope.TenantId == evt.TenantId;
    }

    private sealed record Subscriber(
        PushSubscriptionScope Scope,
        Channel<PushEventEnvelope> Channel);
}
