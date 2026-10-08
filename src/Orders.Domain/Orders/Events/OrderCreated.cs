using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders.Events;

public record OrderStarted : DomainEvent
{
    public OrderStarted(Guid orderId, decimal total, List<OrderStartedItem> items)
    {
        OrderId = orderId;
        Total = total;
        Items = items;
    }
    public Guid OrderId { get; init; }
    public decimal Total { get; init; }

    public List<OrderStartedItem> Items { get; init; }

    public OrderStarted ()
    {
        Items = [];
    }
}

public record OrderStartedItem : DomainEvent
{
    public Guid OrderId { get; init; }
    public Guid ProductId { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public OrderStartedItem(Guid orderId, Guid productId, decimal price, int quantity)
    {
        OrderId = orderId;
        ProductId = productId;
        Price = price;
        Quantity = quantity;
    }
    public OrderStartedItem() { }
}
