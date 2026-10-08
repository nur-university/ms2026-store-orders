using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.EFCore.Persistence;
using Orders.Infrastructure.Persistence.StoredModel.Entities;

namespace Orders.Infrastructure.Persistence.StoredModel;

internal class StoredDbContext(DbContextOptions<StoredDbContext> options) : DbContext(options), IDatabase
{
    public DbSet<OrderStoredModel> Order { get; set; } = null!;
    public DbSet<OrderItemStoredModel> OrderItem { get; set; } = null!;
    public DbSet<ProductStoredModel> Product { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddOutboxModel<DomainEvent>();
    }

    public void Migrate()
    {
        Database.Migrate();
    }
}
