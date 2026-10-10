using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

public record OrderReservedLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderReserved(Guid OrderId, decimal TotalAmount, string? CustomerName = null, string? CustomerDocument = null,
    IReadOnlyList<OrderReservedLine>? Lines = null) : DomainEvent;
