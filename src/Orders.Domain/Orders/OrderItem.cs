using Joseco.DDD.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Domain.Orders;

public class OrderItem : Entity
{
    public Guid ProductId { get; private set; }
    public PriceValue Price { get; private set; }
    public QuantityValue Quantity { get; private set; }

    public Guid OrderId { get; private set; }
    public OrderItem(Guid productId, Guid orderId, decimal price, int quantity)
    {
        ProductId = productId;
        Price = price;
        Quantity = quantity;
        OrderId = orderId;
    }

    /// EF Core constructor
    private OrderItem()
    {

    }
}
