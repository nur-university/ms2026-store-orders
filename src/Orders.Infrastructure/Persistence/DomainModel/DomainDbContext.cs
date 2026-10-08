using Orders.Infrastructure.Inbox;
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
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainDbContext).Assembly);
        modelBuilder.AddOutboxModel<DomainEvent>();
        modelBuilder.AddInboxModel();

        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<DomainEvent>();

        if (Database.IsNpgsql())
        {
            // Optimistic concurrency: two workers changing the same row at once must not overwrite each other.
            modelBuilder.Entity<Order>().Property<uint>("xmin")
                .HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
    }
}
