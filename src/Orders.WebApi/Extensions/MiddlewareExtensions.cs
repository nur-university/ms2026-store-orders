

using Orders.WebApi.Middleware;

namespace Orders.WebApi.Extensions;

public static class MiddlewareExtensions
{   
    public static IApplicationBuilder UseTracing(this IApplicationBuilder app)
    {
        app.UseMiddleware<TracingMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseRequestContextLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestContextLoggingMiddleware>();

        return app;
    }
}
