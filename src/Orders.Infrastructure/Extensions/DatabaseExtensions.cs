using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Service;
using Joseco.Outbox.EFCore;
using Joseco.Outbox.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Domain.Orders;
using Orders.Domain.Products;
using Orders.Infrastructure.Persistence;
using Orders.Infrastructure.Persistence.DomainModel;
using Orders.Infrastructure.Persistence.Repositories;
using Orders.Infrastructure.Persistence.StoredModel;

namespace Orders.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        var databaseSettings = services.BuildServiceProvider().GetRequiredService<DataBaseSettings>();
        var dbConnectionString = databaseSettings.ConnectionString;

        services.AddDbContext<StoredDbContext>(context =>
                context.UseNpgsql(dbConnectionString));
        services.AddDbContext<DomainDbContext>(context =>
                context.UseNpgsql(dbConnectionString));

        services.AddScoped<IDatabase, StoredDbContext>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IOutboxDatabase<DomainEvent>, UnitOfWork>();
        services.AddOutbox<DomainEvent>();

        services.Decorate<IOutboxService<DomainEvent>, OutboxTracingService<DomainEvent>>();

        return services;
    }
}
