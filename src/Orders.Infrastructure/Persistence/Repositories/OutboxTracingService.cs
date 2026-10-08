using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using Nur.Store2026.Observability.Tracing;

namespace Orders.Infrastructure.Persistence.Repositories;

public class OutboxTracingService<T> : IOutboxService<T>
{
    private readonly IOutboxService<T> _baseOutboService;
    private readonly ITracingProvider _tracingProvider;

    public OutboxTracingService(IOutboxService<T> baseOutboService, ITracingProvider tracingProvider)
    {
        _baseOutboService = baseOutboService;
        _tracingProvider = tracingProvider;
    }

    public async Task AddAsync(OutboxMessage<T> message)
    {
        OutboxMessage<T> outboxMessage = new(message.Content,
            _tracingProvider.GetCorrelationId(),
            _tracingProvider.GetTraceId(),
            _tracingProvider.GetSpanId());

        await _baseOutboService.AddAsync(outboxMessage);
    }
}
