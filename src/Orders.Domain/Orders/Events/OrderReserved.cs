using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

public record OrderReserved(Guid OrderId, decimal TotalAmount) : DomainEvent;