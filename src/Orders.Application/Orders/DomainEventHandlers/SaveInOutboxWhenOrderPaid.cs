using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;

namespace Orders.Application.Orders.DomainEventHandlers;

internal class SaveInOutboxWhenOrderPaid : INotificationHandler<Domain.Orders.Events.OrderPaid>
{
    private readonly IOutboxService<DomainEvent> _outboxService;

    public SaveInOutboxWhenOrderPaid(IOutboxService<DomainEvent> outboxService)
    {
        _outboxService = outboxService;
    }

    public async Task Handle(Domain.Orders.Events.OrderPaid domainEvent, CancellationToken cancellationToken)
    {
        OutboxMessage<DomainEvent> outboxMessage = new(domainEvent);
        await _outboxService.AddAsync(outboxMessage);
    }
}
