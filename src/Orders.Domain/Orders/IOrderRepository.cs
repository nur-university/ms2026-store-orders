using Joseco.DDD.Core.Abstractions;

namespace Orders.Domain.Orders;

public interface IOrderRepository : IRepository<Order>
{
    /// <summary>Ids and status of orders stuck in an intermediate status since before the given instants.</summary>
    Task<IReadOnlyList<(Guid OrderId, OrderStatus Status)>> GetStaleAsync(
        DateTime startedBefore, DateTime reservedBefore, DateTime paymentBefore, int take);
}
