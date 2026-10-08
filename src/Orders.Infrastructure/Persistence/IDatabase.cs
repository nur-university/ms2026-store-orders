namespace Orders.Infrastructure.Persistence;

public interface IDatabase : IDisposable
{
    void Migrate();
}
