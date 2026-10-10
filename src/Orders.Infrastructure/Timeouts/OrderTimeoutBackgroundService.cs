using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orders.Application.Orders.UseCases.ExpireOrder;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Timeouts;

/// <summary>
/// Saga timeout: orders that stay too long in Started, Reserved or PaymentInProcess are failed, which also
/// triggers the stock release compensation when stock had been reserved.
/// </summary>
internal class OrderTimeoutBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderTimeoutOptions> options,
    TimeProvider timeProvider,
    ILogger<OrderTimeoutBackgroundService> logger) : BackgroundService
{
    private readonly OrderTimeoutOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireStaleOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Looking for timed out orders failed");
            }

            try
            {
                await Task.Delay(_options.CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal async Task<int> ExpireStaleOrdersAsync(CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        IReadOnlyList<(Guid OrderId, OrderStatus Status)> stale;
        using (IServiceScope scope = scopeFactory.CreateScope())
        {
            stale = await scope.ServiceProvider.GetRequiredService<IOrderRepository>().GetStaleAsync(
                now - _options.StartedTimeout, now - _options.ReservedTimeout, now - _options.PaymentTimeout, _options.BatchSize);
        }

        int expired = 0;
        foreach ((Guid orderId, OrderStatus status) in stale)
        {
            TimeSpan limit = status switch
            {
                OrderStatus.Started => _options.StartedTimeout,
                OrderStatus.Reserved => _options.ReservedTimeout,
                _ => _options.PaymentTimeout
            };

            // One scope (and DbContext) per order so a failure on one cannot affect the others.
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(
                    new ExpireOrderCommand(orderId, status, $"Timeout: the order stayed in {status} for more than {limit}"),
                    cancellationToken);

                if (result.IsSuccess && result.Value)
                {
                    expired++;
                    logger.LogWarning("Order {OrderId} timed out in status {Status}", orderId, status);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // For example a concurrency conflict because the order just progressed; it is looked at again next round.
                logger.LogWarning(ex, "Order {OrderId} could not be expired", orderId);
            }
        }

        return expired + await DiscardStaleDraftsAsync(now, cancellationToken);
    }

    private async Task<int> DiscardStaleDraftsAsync(DateTime now, CancellationToken cancellationToken)
    {
        DateTime untouchedSince = now - _options.DraftExpiry;

        IReadOnlyList<Guid> drafts;
        using (IServiceScope scope = scopeFactory.CreateScope())
        {
            drafts = await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
                .GetStaleDraftIdsAsync(untouchedSince, _options.BatchSize);
        }

        int discarded = 0;
        foreach (Guid draftId in drafts)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new ExpireDraftCommand(draftId, untouchedSince), cancellationToken);

                if (result.IsSuccess && result.Value)
                {
                    discarded++;
                    logger.LogInformation("Draft {OrderId} was discarded after {Expiry} without changes", draftId, _options.DraftExpiry);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Draft {OrderId} could not be discarded", draftId);
            }
        }

        return discarded;
    }
}
