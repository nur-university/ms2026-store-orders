using Joseco.Communication.External.Contracts.Message;
using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Infrastructure.Persistence.DomainModel;

namespace Orders.Infrastructure.Inbox;

/// <summary>
/// Makes a consumer idempotent: a message (by <see cref="IntegrationMessage.Id"/>) is handled once per consumer.
/// The inbox row is saved together with the handler's own changes in the same SaveChanges.
/// </summary>
internal class IdempotentConsumer<T>(
    IIntegrationMessageConsumer<T> inner,
    DomainDbContext dbContext,
    ILogger<IdempotentConsumer<T>> logger) : IIntegrationMessageConsumer<T>
    where T : IntegrationMessage
{
    private static readonly string ConsumerName = typeof(T).Name;

    public async Task HandleAsync(T message, CancellationToken cancellationToken)
    {
        if (await dbContext.InboxMessages.AnyAsync(
                m => m.MessageId == message.Id && m.Consumer == ConsumerName, cancellationToken))
        {
            logger.LogInformation("{Message} {MessageId} was already processed; ignoring the duplicate", ConsumerName, message.Id);
            return;
        }

        dbContext.InboxMessages.Add(new InboxMessage
        {
            MessageId = message.Id,
            Consumer = ConsumerName,
            ProcessedOn = DateTime.UtcNow
        });

        await inner.HandleAsync(message, cancellationToken);

        // The handler's unit of work normally saved the inbox row with its own changes. When it did not save
        // (ignored message) or discarded the tracker after handling a failure itself, record it now.
        bool tracked = dbContext.ChangeTracker.Entries<InboxMessage>()
            .Any(e => e.Entity.MessageId == message.Id && e.Entity.Consumer == ConsumerName);
        if (!tracked && !await dbContext.InboxMessages.AnyAsync(
                m => m.MessageId == message.Id && m.Consumer == ConsumerName, cancellationToken))
        {
            dbContext.InboxMessages.Add(new InboxMessage
            {
                MessageId = message.Id,
                Consumer = ConsumerName,
                ProcessedOn = DateTime.UtcNow
            });
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
