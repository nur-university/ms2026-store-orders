using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;

namespace Orders.Application.Orders.DomainEventHandlers;

internal class SaveInOutboxWhenOrderPaymentCancellationRequested : INotificationHandler<Domain.Orders.Events.OrderPaymentCancellationRequested>
{
    private readonly IOutboxService<DomainEvent> _outboxService;

    public SaveInOutboxWhenOrderPaymentCancellationRequested(IOutboxService<DomainEvent> outboxService)
    {
        _outboxService = outboxService;
    }

    public async Task Handle(Domain.Orders.Events.OrderPaymentCancellationRequested domainEvent, CancellationToken cancellationToken)
    {
        OutboxMessage<DomainEvent> outboxMessage = new(domainEvent);
        await _outboxService.AddAsync(outboxMessage);
    }
}
