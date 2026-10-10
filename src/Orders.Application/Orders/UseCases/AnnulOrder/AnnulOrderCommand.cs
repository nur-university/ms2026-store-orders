using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.AnnulOrder;

/// <summary>
/// Manual intervention of a supervisor on a paid sale whose goods were never picked up: the reservation is released and the
/// sale is closed, flagged so the money is given back outside the system.
/// </summary>
public record AnnulOrderCommand(Guid OrderId, string Reason) : IRequest<Result<Guid>>;

internal class AnnulOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork) : IRequestHandler<AnnulOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AnnulOrderCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<Guid>(OrderErrors.AnnulReasonRequired());
        }

        Order? order = await orders.GetByIdAsync(request.OrderId);
        if (order == null)
        {
            return Result.Failure<Guid>(OrderErrors.OrderNotFound(request.OrderId));
        }

        try
        {
            order.Annul(request.Reason.Trim());
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}
