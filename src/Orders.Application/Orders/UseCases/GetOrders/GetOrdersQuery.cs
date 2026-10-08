using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Application.Orders.UseCases.GetOrderById;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.GetOrders;

/// <param name="IsStaff">Staff list every order; everybody else only their own.</param>
public record GetOrdersQuery(Guid UserId, bool IsStaff, int Page = 1, int PageSize = 20) : IRequest<Result<PagedOrders>>;

public record PagedOrders(IReadOnlyList<OrderDto> Items, int Page, int PageSize, int Total);

internal class GetOrdersHandler(IOrderRepository orderRepository) : IRequestHandler<GetOrdersQuery, Result<PagedOrders>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedOrders>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        (IReadOnlyList<Order> orders, int total) = await orderRepository.GetPageAsync(
            request.IsStaff ? null : request.UserId, (page - 1) * pageSize, pageSize);

        return new PagedOrders(orders.Select(OrderDto.From).ToList(), page, pageSize, total);
    }
}
