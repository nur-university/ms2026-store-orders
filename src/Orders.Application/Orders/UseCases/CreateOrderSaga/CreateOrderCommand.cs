using Joseco.DDD.Core.Results;
using MediatR;

namespace Orders.Application.Orders.CreateOrder;

/// <param name="UserId">The authenticated customer; it always comes from the access token, never from the request body.</param>
public record CreateOrderCommand(Guid UserId, ICollection<CreateOrderItem> Items) : IRequest<Result<Guid>>;

/// <summary>Prices are not part of the request: they are taken from the catalog data Orders already holds.</summary>
public record CreateOrderItem(Guid ProductId, int Quantity);
