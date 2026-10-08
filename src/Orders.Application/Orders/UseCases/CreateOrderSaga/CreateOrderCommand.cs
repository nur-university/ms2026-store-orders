using Joseco.DDD.Core.Results;
using MediatR;

namespace Orders.Application.Orders.CreateOrder;

public record CreateOrderCommand(ICollection<CreateOrderItem> Items) : IRequest<Result<Guid>>;

public record CreateOrderItem(Guid ProductId, decimal Price, int Quantity);
