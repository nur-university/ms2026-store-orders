using Joseco.DDD.Core.Results;
using Orders.Domain.Orders;
using Orders.Domain.Products;

namespace Orders.Application.Orders;

/// <summary>
/// Turns what the advisor asked for (product and quantity) into sale lines priced from the catalog data Orders holds.
/// The client never supplies a price.
/// </summary>
public class OrderLinesPricer(IProductRepository productRepository)
{
    /// <param name="allowEmpty">A draft may have no lines yet; a sale being started may not.</param>
    public async Task<Result<List<OrderLineInput>>> PriceAsync(
        IEnumerable<(Guid ProductId, int Quantity)>? requested, bool allowEmpty)
    {
        List<(Guid ProductId, int Quantity)> items = requested?.ToList() ?? [];

        if (items.Count == 0)
        {
            return allowEmpty
                ? new List<OrderLineInput>()
                : Result.Failure<List<OrderLineInput>>(OrderErrors.EmptyOrder());
        }

        if (items.Any(i => i.Quantity <= 0))
        {
            return Result.Failure<List<OrderLineInput>>(OrderErrors.QuantityInvalid());
        }

        // The same product listed twice is one line with the summed quantity.
        List<(Guid ProductId, int Quantity)> lines = items
            .GroupBy(i => i.ProductId)
            .Select(g => (g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(lines.Select(l => l.ProductId));

        List<OrderLineInput> priced = [];
        foreach ((Guid productId, int quantity) in lines)
        {
            Product? product = products.FirstOrDefault(p => p.Id == productId);
            if (product == null)
            {
                return Result.Failure<List<OrderLineInput>>(OrderErrors.ProductNotFound(productId));
            }

            if (product.Price is not > 0)
            {
                return Result.Failure<List<OrderLineInput>>(OrderErrors.ProductPriceUnavailable(productId));
            }

            priced.Add(new OrderLineInput(productId, product.Name, product.Price.Value, quantity));
        }

        return priced;
    }
}
