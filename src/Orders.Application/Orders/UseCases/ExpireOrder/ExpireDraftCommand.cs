using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.ExpireOrder;

/// <summary>A draft nobody touched for too long is discarded. If it was edited meanwhile, nothing happens.</summary>
public record ExpireDraftCommand(Guid OrderId, DateTime UntouchedSince) : IRequest<Result<bool>>;

internal class ExpireDraftHandler(IOrderRepository orders, IUnitOfWork unitOfWork) : IRequestHandler<ExpireDraftCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ExpireDraftCommand request, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(request.OrderId);
        if (order == null || order.Status != OrderStatus.Draft || order.UpdatedOn >= request.UntouchedSince)
        {
            return false;
        }

        order.Discard();
        await unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
