using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Application.Orders.DomainEventHandlers;

internal class SaveInOutboxWhenOrderReserved : INotificationHandler<Domain.Orders.Events.OrderReserved>
{
    private readonly IOutboxService<DomainEvent> _outboxService;
    private readonly IUnitOfWork _unitOfWork;

    public SaveInOutboxWhenOrderReserved(IOutboxService<DomainEvent> outboxService, IUnitOfWork unitOfWork)
    {
        _outboxService = outboxService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(Domain.Orders.Events.OrderReserved domainEvent, CancellationToken cancellationToken)
    {
        OutboxMessage<DomainEvent> outboxMessage = new(domainEvent);
        await _outboxService.AddAsync(outboxMessage);

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
