using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Orders.Infrastructure.Persistence.StoredModel;

namespace Orders.Infrastructure.Persistence.Design;

internal class DbContextFactory : IDesignTimeDbContextFactory<StoredDbContext>
{
    public StoredDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        DataBaseSettings dataBaseSettings = new();
        configuration.Bind("OrdersDatabaseConnectionString", dataBaseSettings);
        var connectionString = dataBaseSettings.ConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<StoredDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new StoredDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables();

        // Aquí puedes agregar lógica condicional para consultar Vault si se requiere

        return builder.Build();
    }
}
