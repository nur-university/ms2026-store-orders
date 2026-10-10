using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders;

public class OrderItem : Entity
{
    public Guid ProductId { get; private set; }

    /// <summary>Name of the product when the line was written; invoices must not change if the catalog is edited later.</summary>
    public string ProductName { get; private set; } = string.Empty;
    public PriceValue Price { get; private set; }
    public QuantityValue Quantity { get; private set; }
    public Guid OrderId { get; private set; }

    public OrderItem(Guid productId, Guid orderId, decimal price, int quantity, string productName = "")
    {
        ProductId = productId;
        ProductName = productName;
        Price = price;
        Quantity = quantity;
        OrderId = orderId;
    }

    /// EF Core constructor
    private OrderItem()
    {
    }
}
