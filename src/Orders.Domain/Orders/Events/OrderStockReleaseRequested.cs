using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

/// <summary>
/// Compensation event: the order had stock reserved but will not be fulfilled.
/// </summary>
public record OrderStockReleaseRequested(Guid OrderId, string Reason) : DomainEvent;
