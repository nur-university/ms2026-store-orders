using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;

namespace Orders.Application.Orders.DomainEventHandlers;

internal class SaveInOutboxWhenOrderStockReleaseRequested : INotificationHandler<Domain.Orders.Events.OrderStockReleaseRequested>
{
    private readonly IOutboxService<DomainEvent> _outboxService;

    public SaveInOutboxWhenOrderStockReleaseRequested(IOutboxService<DomainEvent> outboxService)
    {
        _outboxService = outboxService;
    }

    public async Task Handle(Domain.Orders.Events.OrderStockReleaseRequested domainEvent, CancellationToken cancellationToken)
    {
        OutboxMessage<DomainEvent> outboxMessage = new(domainEvent);
        await _outboxService.AddAsync(outboxMessage);
    }
}
