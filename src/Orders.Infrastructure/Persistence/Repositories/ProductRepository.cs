using Joseco.DDD.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Orders.Domain.Products;
using Orders.Infrastructure.Persistence.DomainModel;

namespace Orders.Infrastructure.Persistence.Repositories;

internal class ProductRepository(DomainDbContext dbContext) : IProductRepository
{
    private readonly DomainDbContext _dbContext = dbContext;

    public async Task AddAsync(Product entity)
    {
        await _dbContext.Products.AddAsync(entity);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        List<Guid> wanted = ids.Distinct().ToList();
        return await _dbContext.Products.AsNoTracking().Where(p => wanted.Contains(p.Id)).ToListAsync();
    }

    public Task<Product?> GetByIdAsync(Guid id, bool readOnly = false)
    {
        if (readOnly)
        {
            return _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        }
        return _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
    }
}
