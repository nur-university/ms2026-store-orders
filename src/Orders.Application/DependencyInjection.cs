using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Abstractions.Behaviors;
using Orders.Application.Orders;
using System.Reflection;

namespace Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>));
        });

        services.AddScoped<OrderLinesPricer>();

        return services;
    }
    
}
