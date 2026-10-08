using Consul;

namespace Orders.WebApi.Infrastructure;

public class ServiceRegistration(ConsulClient consulClient, IConfiguration configuration, ILogger<ServiceRegistration> logger) : IHostedService
{
    private string _registrationId = string.Empty;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? serviceAddress = configuration.GetValue<string?>("ServiceRegistration:Host");
        int? servicePort = configuration.GetValue<int?>("ServiceRegistration:Port");
        string? serviceName = configuration.GetValue<string?>("ServiceRegistration:ServiceId");

        // Consul runs this HTTP check from inside its own container, so "localhost"
        // there means the Consul container itself, not this machine. Docker Desktop
        // maps host.docker.internal to the host, so the check needs that address
        // even though the address stored in Consul (what the gateway reads to
        // forward traffic) must stay "localhost" - the gateway is a host process
        // too, and "host.docker.internal" wouldn't resolve for it.
        string? healthCheckAddress = serviceAddress?.Replace("localhost", "host.docker.internal");

        var registration = new AgentServiceRegistration
        {
            ID = $"{serviceName}-{Guid.NewGuid()}",
            Name = serviceName,
            Address = serviceAddress,
            Port = servicePort == null ? 0 : servicePort.Value,
            Check = new AgentServiceCheck
            {
                HTTP = $"{healthCheckAddress}:{servicePort}/health",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(30),
                Method = "GET",
                TLSSkipVerify = true // serviceAddress may be https://localhost with the ASP.NET Core dev cert (self-signed)
            }
        };
        _registrationId = registration.ID;
        logger.LogInformation("Registrando servicio en Consul...");
        await consulClient.Agent.ServiceRegister(registration, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Desregistrando servicio de Consul...");
        if (!string.IsNullOrEmpty(_registrationId))
        {
            await consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
        }
        logger.LogInformation("Servicio desregistrado.");
    }
}
