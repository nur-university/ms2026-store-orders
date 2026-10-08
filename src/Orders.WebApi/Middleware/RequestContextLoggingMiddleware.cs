using Nur.Store2026.Observability.Tracing;
using Orders.Application.Abstractions;
using Serilog.Context;

namespace Orders.WebApi.Middleware;

public class RequestContextLoggingMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context, ITracingProvider tracingProvider)
    {
        using (LogContext.PushProperty("CorrelationId", tracingProvider.GetCorrelationId()))
        {
            return next.Invoke(context);
        }
    }
}
