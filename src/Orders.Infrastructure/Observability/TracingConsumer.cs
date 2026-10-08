using Joseco.Communication.External.Contracts.Message;
using Joseco.Communication.External.Contracts.Services;
using Nur.Store2026.Observability.Tracing;
using System.Diagnostics;

namespace Orders.Infrastructure.Observability
{
    public class TracingConsumer<TMessage> : IIntegrationMessageConsumer<TMessage>
        where TMessage : IntegrationMessage
    {
        private readonly ITracingProvider _tracingProvider;
        private readonly IIntegrationMessageConsumer<TMessage> _decoratedConsumer;

        public TracingConsumer(ITracingProvider tracingProvider, IIntegrationMessageConsumer<TMessage> decoratedConsumer)
        {
            _tracingProvider = tracingProvider;
            _decoratedConsumer = decoratedConsumer;
        }

        public async Task HandleAsync(TMessage message, CancellationToken cancellationToken)
        {
            var activity = Activity.Current;

            if (activity != null)
            {
                _tracingProvider.SetTraceId(activity.TraceId.ToString());
                _tracingProvider.SetSpanId(activity.SpanId.ToString());
            }
            if(message.CorrelationId != null)
                _tracingProvider.SetCorrelationId(message.CorrelationId);

            await _decoratedConsumer.HandleAsync(message, cancellationToken);

        }
    } 
}
