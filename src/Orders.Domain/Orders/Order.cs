using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Orders.Domain.Orders.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Orders.Domain.Orders;

public class Order : AggregateRoot
{
    public DateTime StartedOn { get; private set; }
    public PriceValue TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderItem> _items;

    public DateTime? ReservedOn { get; private set; }
    public DateTime? ProcesingPaymentOn { get; private set; }
    public DateTime? PaidOn { get; private set; }

    public DateTime? CanceledOn { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public DateTime? FailedOn { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>The customer who placed the order (null only for orders created before users were tracked).</summary>
    public Guid? UserId { get; private set; }

    /// <summary>The customer named on the sales note. Walk-in customers have no account, so this is just text.</summary>
    public string? CustomerName { get; private set; }
    public string? CustomerDocument { get; private set; }

    /// <summary>Number of the invoice issued when the order was paid at the cashier.</summary>
    public string? InvoiceNumber { get; private set; }

    public IReadOnlyCollection<OrderItem> Items
    {
        get
        {
            return _items;
        }
    }

    internal Order() : base(Guid.NewGuid())
    {
        StartedOn = DateTime.UtcNow;
        TotalAmount = 0;
        Status = OrderStatus.Started;
        _items = [];
    }

    public void AddItem(Guid productId, decimal price, int quantity)
    {
        OrderItem item = new(productId, Id, price, quantity);
        _items.Add(item);
        TotalAmount += item.Price * item.Quantity;
    }

    public void MarkAsReserved()
    {
        if (Status != OrderStatus.Started)
        {
            throw new DomainException(OrderErrors.ReservedStatusNotAllowed(Status));
        }
        Status = OrderStatus.Reserved;
        ReservedOn = DateTime.UtcNow;

        AddDomainEvent(new OrderReserved(Id, TotalAmount, CustomerName, CustomerDocument));
    }

    public void MarkAsPaymentInProcess()
    {
        if (Status != OrderStatus.Reserved)
        {
            throw new DomainException(OrderErrors.PaymentInProcessStatusNotAllowed(Status));
        }
        Status = OrderStatus.PaymentInProcess;
        ProcesingPaymentOn = DateTime.UtcNow;
    }

    public void MarkAsPaid(string? invoiceNumber = null)
    {
        if (Status != OrderStatus.PaymentInProcess)
        {
            throw new DomainException(OrderErrors.PaidStatusNotAllowed(Status));
        }
        Status = OrderStatus.Paid;
        PaidOn = DateTime.UtcNow;
        InvoiceNumber = invoiceNumber;

        AddDomainEvent(new OrderPaid(Id, invoiceNumber));
    }

    public void MarkAsCompleted()
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException(OrderErrors.CompletedStatusNotAllowed(Status));
        }
        Status = OrderStatus.Completed;
        CompletedOn = DateTime.UtcNow;
    }

    /// <summary>Cancels an order that has not been paid yet; once paid, the stock is already confirmed and a refund would be needed.</summary>
    public void MarkAsCanceled()
    {
        if (Status != OrderStatus.Started && Status != OrderStatus.Reserved && Status != OrderStatus.PaymentInProcess)
        {
            throw new DomainException(OrderErrors.CanceledStatusNotAllowed(Status));
        }
        bool hasReservedStock = HasReservedStock();
        Status = OrderStatus.Canceled;
        CanceledOn = DateTime.UtcNow;

        if (hasReservedStock)
        {
            AddDomainEvent(new OrderStockReleaseRequested(Id, "Order canceled"));
            AddDomainEvent(new OrderPaymentCancellationRequested(Id, "Order canceled"));
        }
    }

    public void MarkAsFailed(string failureReason)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Canceled || Status == OrderStatus.Failed)
        {
            throw new DomainException(OrderErrors.FailedStatusNotAllowed(Status));
        }
        bool hasReservedStock = HasReservedStock();
        Status = OrderStatus.Failed;
        FailedOn = DateTime.UtcNow;
        FailureReason = failureReason;

        // Compensation: inventory must give back the stock it reserved for this order.
        if (hasReservedStock)
        {
            AddDomainEvent(new OrderStockReleaseRequested(Id, failureReason));
            // A payment may already be pending for this order; it must not be collectable any more.
            AddDomainEvent(new OrderPaymentCancellationRequested(Id, failureReason));
        }
    }

    /// <summary>
    /// Asks inventory to release stock that was reserved after this order had already been closed
    /// (for example InventoryReserved arriving after a timeout). The status does not change.
    /// </summary>
    public void RequestStockRelease(string reason)
    {
        if (Status != OrderStatus.Failed && Status != OrderStatus.Canceled)
        {
            throw new DomainException(OrderErrors.StockReleaseNotAllowed(Status));
        }
        AddDomainEvent(new OrderStockReleaseRequested(Id, reason));
    }

    /// <summary>
    /// Asks payments to cancel a pending payment registered after this order had already been closed
    /// (OrderPaymentInProcessRegistered arriving late). The status does not change.
    /// </summary>
    public void RequestPaymentCancellation(string reason)
    {
        if (Status != OrderStatus.Failed && Status != OrderStatus.Canceled)
        {
            throw new DomainException(OrderErrors.PaymentCancellationNotAllowed(Status));
        }
        AddDomainEvent(new OrderPaymentCancellationRequested(Id, reason));
    }

    // Stock is reserved from Reserved until Paid (once Paid, inventory confirms the exit and it can't be released).
    private bool HasReservedStock() =>
        Status == OrderStatus.Reserved || Status == OrderStatus.PaymentInProcess;

    public static Order Create(List<(Guid productId, decimal price, int quantity)> items, Guid? userId = null,
        string? customerName = null, string? customerDocument = null)
    {
        Order order = new();
        order.UserId = userId;
        order.CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        order.CustomerDocument = string.IsNullOrWhiteSpace(customerDocument) ? null : customerDocument.Trim();

        foreach (var (productId, price, quantity) in items)
        {
            order.AddItem(productId, price, quantity);
        }

        List<OrderStartedItem> orderCreatedItems = items
            .Select(i => new OrderStartedItem(order.Id, i.productId, i.price, i.quantity))
            .ToList();

        OrderStarted orderCreated = new(order.Id, order.TotalAmount, orderCreatedItems, userId);

        order.AddDomainEvent(orderCreated);
        return order;
    }

}
