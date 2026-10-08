using Joseco.Communication.External.RabbitMQ;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Nur.Store2026.Observability;
using Nur.Store2026.Observability.Config;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Extensions;

public static class ObservabilityExtensions
{

    public static IServiceCollection AddObservability(this IServiceCollection services, 
        IHostEnvironment environment, string serviceName)
    {

        var jaegerSettings = services.BuildServiceProvider().GetRequiredService<JeagerSettings>();
        bool isWebApp = environment is IWebHostEnvironment;

        services.AddObservability(serviceName, jaegerSettings,
            builder =>
            {
                builder.AddSource("Joseco.Outbox")
                    .AddSource("Joseco.Communication.RabbitMQ")
                    .AddNpgsql();
            },
            shouldIncludeHttpInstrumentation: isWebApp);

        if (isWebApp)
        {
            services.AddServicesHealthChecks();
        }

        return services;
    }

    private static IServiceCollection AddServicesHealthChecks(this IServiceCollection services)
    {
        var databaseSettings = services.BuildServiceProvider().GetRequiredService<DataBaseSettings>();
        string? dbConnectionString = databaseSettings.ConnectionString;

        services
            .AddHealthChecks()
            .AddNpgSql(dbConnectionString)
            .AddRabbitMqHealthCheck();

        return services;
    }
}
