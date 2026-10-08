using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.GetOrderById;

/// <param name="IsStaff">Staff may see any order; everybody else only their own.</param>
public record GetOrderByIdQuery(Guid OrderId, Guid UserId, bool IsStaff) : IRequest<Result<OrderDto>>;

internal class GetOrderByIdHandler(IOrderRepository orderRepository) : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        Order? order = await orderRepository.GetByIdAsync(request.OrderId, readOnly: true);

        // Somebody else's order is reported as not found so its existence is not revealed.
        if (order == null || (!request.IsStaff && order.UserId != request.UserId))
        {
            return Result.Failure<OrderDto>(OrderErrors.OrderNotFound(request.OrderId));
        }

        return OrderDto.From(order);
    }
}
