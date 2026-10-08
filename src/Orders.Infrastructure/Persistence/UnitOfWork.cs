using Orders.Infrastructure.Persistence.DomainModel;
using MediatR;
using Joseco.DDD.Core.Abstractions;
using System.Collections.Immutable;
using Joseco.Outbox.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Joseco.Outbox.Contracts.Model;

namespace Orders.Infrastructure.Persistence
{
    internal class UnitOfWork : IUnitOfWork, IOutboxDatabase<DomainEvent>
    {
        private readonly DomainDbContext _dbContext;
        private readonly IMediator _mediator;

        private int _transactionCount = 0;

        public UnitOfWork(DomainDbContext dbContext, IMediator mediator)
        {
            _dbContext = dbContext;
            _mediator = mediator;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            _transactionCount++;

            var domainEvents = _dbContext.ChangeTracker
                .Entries<Entity>()
                .Where(x => x.Entity.DomainEvents.Any())
                .Select(x =>
                {
                    var domainEvents = x.Entity
                                    .DomainEvents
                                    .ToImmutableArray();
                    x.Entity.ClearDomainEvents();

                    return domainEvents;
                })
                .SelectMany(domainEvents => domainEvents)
                .ToList();

            foreach (var e in domainEvents)
            {
                await _mediator.Publish(e, cancellationToken);

            }

            if(_transactionCount == 1)
            {
                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _dbContext.ChangeTracker.Clear();
                    throw;
                }
                catch (DbUpdateException)
                {
                    _dbContext.ChangeTracker.Clear();
                    throw;
                }
            }
            else
            {
                _transactionCount--;
            }

               
        }

        public DbSet<OutboxMessage<DomainEvent>> GetOutboxMessages()
        {
            return _dbContext.OutboxMessages;
        }
    }
}
