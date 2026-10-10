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

    /// <summary>Last change of a draft; drafts that nobody touches are discarded after a while.</summary>
    public DateTime UpdatedOn { get; private set; }

    /// <summary>Why a paid order was annulled by a supervisor, or why the order was canceled.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>The sale was invoiced and paid before being annulled, so the money has to be given back outside the system.</summary>
    public bool RefundRequired { get; private set; }

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
        UpdatedOn = StartedOn;
        TotalAmount = 0;
        Status = OrderStatus.Started;
        _items = [];
    }

    public void AddItem(Guid productId, decimal price, int quantity) => AddItem(productId, string.Empty, price, quantity);

    public void AddItem(Guid productId, string productName, decimal price, int quantity)
    {
        OrderItem item = new(productId, Id, price, quantity, productName);
        _items.Add(item);
        TotalAmount += item.Price * item.Quantity;
    }

    private void ReplaceItems(IEnumerable<OrderLineInput> lines)
    {
        _items.Clear();
        TotalAmount = 0;
        foreach (OrderLineInput line in lines)
        {
            AddItem(line.ProductId, line.ProductName, line.Price, line.Quantity);
        }
    }

    // ------------------------------------------------------------- draft

    /// <summary>
    /// Starts a sales note that is still being written: it holds lines but reserves no stock and creates no charge order.
    /// </summary>
    public static Order CreateDraft(Guid userId, string? customerName = null, string? customerDocument = null)
    {
        Order order = new() { UserId = userId };
        order.Status = OrderStatus.Draft;
        order.SetCustomerData(customerName, customerDocument);
        return order;
    }

    /// <summary>Replaces the lines of the draft. Prices are an estimate: they are taken again when the draft is confirmed.</summary>
    public void SetDraftLines(IEnumerable<OrderLineInput> lines)
    {
        EnsureDraft();
        ReplaceItems(lines);
        UpdatedOn = DateTime.UtcNow;
    }

    public void SetDraftCustomer(string? customerName, string? customerDocument)
    {
        EnsureDraft();
        SetCustomerData(customerName, customerDocument);
        UpdatedOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Completes the draft: the lines are priced with the given (current) prices and the sale starts, which sets the
    /// whole flow in motion (stock reservation, then the charge order).
    /// </summary>
    public void Confirm(IEnumerable<OrderLineInput> pricedLines)
    {
        EnsureDraft();
        List<OrderLineInput> lines = pricedLines.ToList();
        if (lines.Count == 0)
        {
            throw new DomainException(OrderErrors.EmptyOrder());
        }

        ReplaceItems(lines);
        Status = OrderStatus.Started;
        StartedOn = DateTime.UtcNow; // the saga timeouts count from here, not from when the draft was opened
        UpdatedOn = StartedOn;

        RaiseStarted();
    }

    public void Discard()
    {
        EnsureDraft();
        Status = OrderStatus.Discarded;
        UpdatedOn = DateTime.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != OrderStatus.Draft)
        {
            throw new DomainException(OrderErrors.NotADraft(Status));
        }
    }

    private void SetCustomerData(string? customerName, string? customerDocument)
    {
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        CustomerDocument = string.IsNullOrWhiteSpace(customerDocument) ? null : customerDocument.Trim();
    }

    private void RaiseStarted()
    {
        List<OrderStartedItem> items = _items
            .Select(i => new OrderStartedItem(Id, i.ProductId, i.Price, i.Quantity))
            .ToList();

        AddDomainEvent(new OrderStarted(Id, TotalAmount, items, UserId));
    }

    public void MarkAsReserved()
    {
        if (Status != OrderStatus.Started)
        {
            throw new DomainException(OrderErrors.ReservedStatusNotAllowed(Status));
        }
        Status = OrderStatus.Reserved;
        ReservedOn = DateTime.UtcNow;

        List<OrderReservedLine> lines = _items
            .Select(i => new OrderReservedLine(i.ProductId, i.ProductName, i.Quantity, i.Price))
            .ToList();

        AddDomainEvent(new OrderReserved(Id, TotalAmount, CustomerName, CustomerDocument, lines));
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

        AddDomainEvent(new OrderPaid(Id, invoiceNumber, CustomerName, CustomerDocument));
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

    /// <summary>
    /// Manual intervention of a supervisor on a paid sale whose goods were never picked up: the reservation is released and
    /// the sale is closed. The money has to be given back outside the system (RefundRequired).
    /// </summary>
    public void Annul(string reason)
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException(OrderErrors.AnnulNotAllowed(Status));
        }

        Status = OrderStatus.Canceled;
        CanceledOn = DateTime.UtcNow;
        CancellationReason = reason;
        RefundRequired = true;

        AddDomainEvent(new OrderStockReleaseRequested(Id, reason));
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

    /// <summary>Creates a sale that starts right away (a draft that is confirmed in the same step).</summary>
    public static Order Create(List<(Guid productId, decimal price, int quantity)> items, Guid? userId = null,
        string? customerName = null, string? customerDocument = null) =>
        Create(items.Select(i => new OrderLineInput(i.productId, string.Empty, i.price, i.quantity)), userId, customerName, customerDocument);

    public static Order Create(IEnumerable<OrderLineInput> lines, Guid? userId = null,
        string? customerName = null, string? customerDocument = null)
    {
        Order order = new() { UserId = userId };
        order.SetCustomerData(customerName, customerDocument);
        order.ReplaceItems(lines);
        order.RaiseStarted();
        return order;
    }

}

/// <summary>A line of a sale as it is written or confirmed: product, its name and unit price at that moment, and quantity.</summary>
public record OrderLineInput(Guid ProductId, string ProductName, decimal Price, int Quantity);
