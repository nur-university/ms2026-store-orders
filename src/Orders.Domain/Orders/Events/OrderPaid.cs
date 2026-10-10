using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

public record OrderPaid(Guid OrderId, string? InvoiceNumber = null, string? CustomerName = null, string? CustomerDocument = null) : DomainEvent;
