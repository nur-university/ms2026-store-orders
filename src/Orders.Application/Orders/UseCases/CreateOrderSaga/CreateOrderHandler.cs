using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Orders.Domain.Orders;
using Orders.Domain.Products;

namespace Orders.Application.Orders.CreateOrder;

/// <summary>Starts the order saga: validates the request, prices the items and stores the order.</summary>
public class CreateOrderHandler(IOrderRepository orderRepository, IProductRepository productRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return Result.Failure<Guid>(OrderErrors.EmptyOrder());
        }

        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return Result.Failure<Guid>(OrderErrors.QuantityInvalid());
        }

        // The same product listed twice is one line with the summed quantity.
        List<(Guid ProductId, int Quantity)> lines = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(lines.Select(l => l.ProductId));

        List<(Guid productId, decimal price, int quantity)> items = [];
        foreach ((Guid productId, int quantity) in lines)
        {
            Product? product = products.FirstOrDefault(p => p.Id == productId);
            if (product == null)
            {
                return Result.Failure<Guid>(OrderErrors.ProductNotFound(productId));
            }

            if (product.Price is not > 0)
            {
                return Result.Failure<Guid>(OrderErrors.ProductPriceUnavailable(productId));
            }

            items.Add((productId, product.Price.Value, quantity));
        }

        Order order = Order.Create(items, request.UserId, request.CustomerName, request.CustomerDocument);

        await orderRepository.AddAsync(order);
        await unitOfWork.CommitAsync(cancellationToken);

        return order.Id;
    }
}
