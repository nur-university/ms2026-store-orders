using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.ExpireOrder;

/// <param name="ExpectedStatus">The status the order had when it was found stale; if it moved on meanwhile, nothing happens.</param>
public record ExpireOrderCommand(Guid OrderId, OrderStatus ExpectedStatus, string Reason) : IRequest<Result<bool>>;
