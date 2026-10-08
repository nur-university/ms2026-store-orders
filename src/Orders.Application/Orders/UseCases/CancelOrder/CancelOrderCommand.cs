using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.CancelOrder;

/// <param name="IsStaff">Staff may cancel any order; everybody else only their own.</param>
public record CancelOrderCommand(Guid OrderId, Guid UserId, bool IsStaff) : IRequest<Result<Guid>>;

internal class CancelOrderHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<CancelOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        Order? order = await orderRepository.GetByIdAsync(request.OrderId);
        if (order == null || (!request.IsStaff && order.UserId != request.UserId))
        {
            return Result.Failure<Guid>(OrderErrors.OrderNotFound(request.OrderId));
        }

        try
        {
            // Releases the reserved stock and cancels a pending payment through outbox events.
            order.MarkAsCanceled();
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return order.Id;
    }
}
