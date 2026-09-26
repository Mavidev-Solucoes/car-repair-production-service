using Application.Common.Interfaces;
using Domain.Common;
using MediatR;

namespace Infrastructure.Messaging;

internal sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(new DomainEventNotification(domainEvent), cancellationToken);
        }
    }
}

public sealed record DomainEventNotification(IDomainEvent DomainEvent) : INotification;
