using Nur.Store2026.Observability.Tracing;
using Orders.Application.Abstractions;
using System.Diagnostics;

namespace Orders.WebApi.Middleware;

public class TracingMiddleware
{
    private readonly RequestDelegate _next;
    private
    const string _correlationIdHeader = "X-Correlation-Id";

    public TracingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITracingProvider tracingProvider)
    {
        var activity = Activity.Current;

        if (activity != null)
        {
            tracingProvider.SetTraceId(activity.TraceId.ToString());
            tracingProvider.SetSpanId(activity.SpanId.ToString());
        }
        // get or generate the request correlation id
        var requestCorrelationId = GetCorrelationId(context, tracingProvider);

        // add the correlation id to the http response header
        AddCorrelationIdHeaderToResponse(context, requestCorrelationId);

        await _next(context);
    }

    private string GetCorrelationId(HttpContext context, ITracingProvider tracingProvider)
    {
        if (context.Request.Headers.TryGetValue(_correlationIdHeader, out
            var existingCorrelationId))
        {
            tracingProvider.SetCorrelationId(existingCorrelationId);
            return existingCorrelationId;
        }
        return tracingProvider.GetCorrelationId();
    }

    private static void AddCorrelationIdHeaderToResponse(HttpContext context, string correlationId)
    {
        context.Response.OnStarting(() => {
            context.Response.Headers[_correlationIdHeader] = correlationId;
            return Task.CompletedTask;
        });
    }
}
