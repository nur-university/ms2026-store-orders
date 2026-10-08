using Consul;
using HealthChecks.UI.Client;
using Orders.WebApi.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

namespace Orders.WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Ingresar el token JWT obtenido desde Identity. Solo el token, sin el prefijo 'Bearer '."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // REMARK: If you want to use Controllers, you'll need this.
        services.AddControllers();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Lets the ApiGateway's Swagger UI (a different origin/port) fetch this
        // service's swagger.json directly from the browser. Only enabled in
        // Development (see Program.cs) - never relaxes CORS in production.
        services.AddCors(options =>
            options.AddPolicy("AllowGatewaySwagger", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        services.RegisterServiceToServiceDiscovery(configuration);

        return services;
    }

    private static IServiceCollection RegisterServiceToServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
    {
        string? serviceDiscoveryAddress = configuration.GetValue<string?>("ServiceRegistration:ServiceDiscoveryAddress");

        services.AddSingleton(sp => new ConsulClient(c => c.Address = new Uri(serviceDiscoveryAddress)));
        services.AddHostedService<ServiceRegistration>();

        return services;
    }
}
