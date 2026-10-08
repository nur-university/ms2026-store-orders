using Joseco.DDD.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Domain.Products;

public class Product : AggregateRoot
{
    public string Name { get; private set; }

    /// <summary>Unit price announced by Catalog; null while it is unknown (products created before prices were published).</summary>
    public decimal? Price { get; private set; }

    public Product(Guid productId, string name, decimal? price = null) : base(productId)
    {
        Name = name;
        Price = price;
    }

    public void ChangePrice(decimal price)
    {
        Price = price;
    }

    //Required for EF Core
    private Product() 
    {
        Name = "";
    }
}
