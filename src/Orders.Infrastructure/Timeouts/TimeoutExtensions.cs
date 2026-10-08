using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Orders.UseCases.ExpireOrder;

namespace Orders.Infrastructure.Timeouts;

public static class TimeoutExtensions
{
    /// <summary>Registers the saga timeout job. Only the worker service should call it.</summary>
    public static IServiceCollection AddOrderTimeouts(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OrderTimeoutOptions>(configuration.GetSection(OrderTimeoutOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<OrderTimeoutBackgroundService>();

        return services;
    }
}
