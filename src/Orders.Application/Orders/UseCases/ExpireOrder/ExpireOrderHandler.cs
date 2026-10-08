using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.ExpireOrder;

internal class ExpireOrderHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<ExpireOrderCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ExpireOrderCommand request, CancellationToken cancellationToken)
    {
        Order? order = await orderRepository.GetByIdAsync(request.OrderId);

        // Re-checked here because the order may have progressed since it was listed as stale.
        if (order == null || order.Status != request.ExpectedStatus)
        {
            return false;
        }

        // Failing the order also asks inventory to release the stock when it was already reserved.
        order.MarkAsFailed(request.Reason);
        await unitOfWork.CommitAsync(cancellationToken);

        return true;
    }
}
