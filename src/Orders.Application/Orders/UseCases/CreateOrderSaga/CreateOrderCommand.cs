using Joseco.DDD.Core.Results;
using MediatR;

namespace Orders.Application.Orders.CreateOrder;

/// <param name="UserId">The sales advisor creating the sales note; it always comes from the access token, never from the request body.</param>
/// <param name="CustomerName">Optional name of the customer on the sales note (walk-in customers have no account).</param>
public record CreateOrderCommand(Guid UserId, ICollection<CreateOrderItem> Items,
    string? CustomerName = null, string? CustomerDocument = null) : IRequest<Result<Guid>>;

/// <summary>Prices are not part of the request: they are taken from the catalog data Orders already holds.</summary>
public record CreateOrderItem(Guid ProductId, int Quantity);
