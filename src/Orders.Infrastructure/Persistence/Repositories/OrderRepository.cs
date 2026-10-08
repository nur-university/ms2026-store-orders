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
