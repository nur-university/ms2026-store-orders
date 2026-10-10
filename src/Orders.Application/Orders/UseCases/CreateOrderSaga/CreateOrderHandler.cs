using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.CreateOrder;

/// <summary>Creates a sale and starts it right away: the shortcut for "write the draft and complete it" in one step.</summary>
public class CreateOrderHandler(IOrderRepository orderRepository, OrderLinesPricer pricer, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        Result<List<OrderLineInput>> priced = await pricer.PriceAsync(
            request.Items?.Select(i => (i.ProductId, i.Quantity)), allowEmpty: false);
        if (priced.IsFailure)
        {
            return Result.Failure<Guid>(priced.Error);
        }

        Order order = Order.Create(priced.Value, request.UserId, request.CustomerName, request.CustomerDocument);

        await orderRepository.AddAsync(order);
        await unitOfWork.CommitAsync(cancellationToken);

        return order.Id;
    }
}
