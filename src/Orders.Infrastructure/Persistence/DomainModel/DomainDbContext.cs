using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;
using Orders.Domain.Products;
using Orders.Infrastructure.Persistence.StoredModel.Entities;

namespace Orders.Infrastructure.Persistence.DomainModel;

internal class DomainDbContext(DbContextOptions<DomainDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<OutboxMessage<DomainEvent>> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainDbContext).Assembly);
        modelBuilder.AddOutboxModel<DomainEvent>();

        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<DomainEvent>();
    }
}
