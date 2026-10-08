using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

public record OrderPaid(Guid OrderId) : DomainEvent;
