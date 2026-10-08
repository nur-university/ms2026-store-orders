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

        AddDomainEvent(new OrderReserved(Id, TotalAmount));
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

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.PaymentInProcess)
        {
            throw new DomainException(OrderErrors.PaidStatusNotAllowed(Status));
        }
        Status = OrderStatus.Paid;
        PaidOn = DateTime.UtcNow;
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

    public void MarkAsCanceled()
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Canceled)
        {
            throw new DomainException(OrderErrors.CanceledStatusNotAllowed(Status));
        }
        Status = OrderStatus.Canceled;
        CanceledOn = DateTime.UtcNow;
    }

    public void MarkAsFailed(string failureReason)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Canceled || Status == OrderStatus.Failed)
        {
            throw new DomainException(OrderErrors.FailedStatusNotAllowed(Status));
        }
        Status = OrderStatus.Failed;
        FailedOn = DateTime.UtcNow;
        FailureReason = failureReason;
    }

    public static Order Create(List<(Guid productId, decimal price, int quantity)> items)
    {
        Order order = new();

        foreach (var (productId, price, quantity) in items)
        {
            order.AddItem(productId, price, quantity);
        }

        List<OrderStartedItem> orderCreatedItems = items
            .Select(i => new OrderStartedItem(order.Id, i.productId, i.price, i.quantity))
            .ToList();

        OrderStarted orderCreated = new(order.Id, order.TotalAmount, orderCreatedItems);

        order.AddDomainEvent(orderCreated);
        return order;
    }

}
