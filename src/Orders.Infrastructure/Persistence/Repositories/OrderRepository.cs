using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;
using Orders.Infrastructure.Persistence.DomainModel;

namespace Orders.Infrastructure.Persistence.Repositories;

internal class OrderRepository(DomainDbContext context) : IOrderRepository
{
    private readonly DomainDbContext _context = context;
    public async Task AddAsync(Order entity)
    {
        await _context.Orders.AddAsync(entity);
    }

    public async Task<(IReadOnlyList<Order> Orders, int Total)> GetPageAsync(Guid? userId, int skip, int take)
    {
        IQueryable<Order> query = _context.Orders.AsNoTracking();
        if (userId.HasValue)
        {
            query = query.Where(o => o.UserId == userId.Value);
        }

        int total = await query.CountAsync();
        List<Order> page = await query.Include("_items").OrderByDescending(o => o.StartedOn).Skip(skip).Take(take).ToListAsync();

        return (page, total);
    }

    public async Task<IReadOnlyList<(Guid OrderId, OrderStatus Status)>> GetStaleAsync(
        DateTime startedBefore, DateTime reservedBefore, DateTime paymentBefore, int take)
    {
        var stale = await _context.Orders.AsNoTracking()
            .Where(o => (o.Status == OrderStatus.Started && o.StartedOn < startedBefore)
                     || (o.Status == OrderStatus.Reserved && o.ReservedOn < reservedBefore)
                     || (o.Status == OrderStatus.PaymentInProcess && o.ProcesingPaymentOn < paymentBefore))
            .OrderBy(o => o.StartedOn)
            .Select(o => new { o.Id, o.Status })
            .Take(take)
            .ToListAsync();

        return stale.Select(o => (o.Id, o.Status)).ToList();
    }

    public Task<Order?> GetByIdAsync(Guid id, bool readOnly = false)
    {
        if (readOnly)
        {
            return _context.Orders.AsNoTracking().Include("_items").FirstOrDefaultAsync(o => o.Id == id);
        }
        else
        {
            return _context.Orders.Include("_items").FirstOrDefaultAsync(o => o.Id == id);
        }
    }
}
