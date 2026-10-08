using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

/// <summary>
/// Compensation event: the order was closed while a payment may be pending, so payments must cancel it.
/// </summary>
public record OrderPaymentCancellationRequested(Guid OrderId, string Reason) : DomainEvent;
