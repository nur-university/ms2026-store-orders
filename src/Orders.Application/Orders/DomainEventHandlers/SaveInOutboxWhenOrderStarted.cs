using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;

namespace Orders.Application.Orders.DomainEventHandlers;

internal class SaveInOutboxWhenOrderStarted : INotificationHandler<Domain.Orders.Events.OrderStarted>
{
    private readonly IOutboxService<DomainEvent> _outboxService;
    private readonly IUnitOfWork _unitOfWork;    

    public SaveInOutboxWhenOrderStarted(IOutboxService<DomainEvent> outboxService, IUnitOfWork unitOfWork)
    {        
        _outboxService = outboxService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(Domain.Orders.Events.OrderStarted domainEvent, CancellationToken cancellationToken)
    {
        OutboxMessage<DomainEvent> outboxMessage = new(domainEvent);
        await _outboxService.AddAsync(outboxMessage);

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
