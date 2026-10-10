using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Application.Orders.CreateOrder;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.Drafts;

/// <summary>Opens a sales note that is still being written. Nothing is reserved and no charge order exists yet.</summary>
public record CreateDraftCommand(Guid UserId, ICollection<CreateOrderItem>? Items = null,
    string? CustomerName = null, string? CustomerDocument = null) : IRequest<Result<Guid>>;

/// <summary>Replaces the lines of a draft.</summary>
public record UpdateDraftItemsCommand(Guid OrderId, Guid UserId, ICollection<CreateOrderItem> Items) : IRequest<Result<Guid>>;

public record UpdateDraftCustomerCommand(Guid OrderId, Guid UserId, string? CustomerName, string? CustomerDocument) : IRequest<Result<Guid>>;

/// <summary>Completes the draft: it is priced with the current prices and the sale starts.</summary>
public record ConfirmDraftCommand(Guid OrderId, Guid UserId) : IRequest<Result<Guid>>;

public record DiscardDraftCommand(Guid OrderId, Guid UserId) : IRequest<Result<Guid>>;

/// <summary>Drafts are private to the advisor who wrote them; somebody else's draft looks like it does not exist.</summary>
internal static class DraftAccess
{
    public static async Task<Order?> LoadOwnedAsync(IOrderRepository orders, Guid orderId, Guid userId)
    {
        Order? order = await orders.GetByIdAsync(orderId);
        return order != null && order.UserId == userId ? order : null;
    }
}

internal class CreateDraftHandler(IOrderRepository orders, OrderLinesPricer pricer, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateDraftCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDraftCommand request, CancellationToken cancellationToken)
    {
        Result<List<OrderLineInput>> lines = await pricer.PriceAsync(
            request.Items?.Select(i => (i.ProductId, i.Quantity)), allowEmpty: true);
        if (lines.IsFailure)
        {
            return Result.Failure<Guid>(lines.Error);
        }

        Order draft = Order.CreateDraft(request.UserId, request.CustomerName, request.CustomerDocument);
        draft.SetDraftLines(lines.Value);

        await orders.AddAsync(draft);
        await unitOfWork.CommitAsync(cancellationToken);

        return draft.Id;
    }
}

internal class UpdateDraftItemsHandler(IOrderRepository orders, OrderLinesPricer pricer, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDraftItemsCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpdateDraftItemsCommand request, CancellationToken cancellationToken)
    {
        Order? order = await DraftAccess.LoadOwnedAsync(orders, request.OrderId, request.UserId);
        if (order == null)
        {
            return Result.Failure<Guid>(OrderErrors.OrderNotFound(request.OrderId));
        }

        Result<List<OrderLineInput>> lines = await pricer.PriceAsync(
            request.Items?.Select(i => (i.ProductId, i.Quantity)), allowEmpty: true);
        if (lines.IsFailure)
        {
            return Result.Failure<Guid>(lines.Error);
        }

        try
        {
            order.SetDraftLines(lines.Value);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}

internal class UpdateDraftCustomerHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDraftCustomerCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpdateDraftCustomerCommand request, CancellationToken cancellationToken)
    {
        Order? order = await DraftAccess.LoadOwnedAsync(orders, request.OrderId, request.UserId);
        if (order == null)
        {
            return Result.Failure<Guid>(OrderErrors.OrderNotFound(request.OrderId));
        }

        try
        {
            order.SetDraftCustomer(request.CustomerName, request.CustomerDocument);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}

internal class ConfirmDraftHandler(IOrderRepository orders, OrderLinesPricer pricer, IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmDraftCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ConfirmDraftCommand request, CancellationToken cancellationToken)
    {
        Order? order = await DraftAccess.LoadOwnedAsync(orders, request.OrderId, request.UserId);
        if (order == null)
        {
            return Result.Failure<Guid>(OrderErrors.OrderNotFound(request.OrderId));
        }

        if (order.Status != OrderStatus.Draft)
        {
            return Result.Failure<Guid>(OrderErrors.NotADraft(order.Status));
        }

        // The prices of the draft were only an estimate: the sale is priced now.
        Result<List<OrderLineInput>> priced = await pricer.PriceAsync(
            order.Items.Select(i => (i.ProductId, (int)i.Quantity)), allowEmpty: false);
        if (priced.IsFailure)
        {
            return Result.Failure<Guid>(priced.Error);
        }

        try
        {
            order.Confirm(priced.Value);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}

internal class DiscardDraftHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : IRequestHandler<DiscardDraftCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(DiscardDraftCommand request, CancellationToken cancellationToken)
    {
        Order? order = await DraftAccess.LoadOwnedAsync(orders, request.OrderId, request.UserId);
        if (order == null)
        {
            return Result.Failure<Guid>(OrderErrors.OrderNotFound(request.OrderId));
        }

        try
        {
            order.Discard();
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }
}
