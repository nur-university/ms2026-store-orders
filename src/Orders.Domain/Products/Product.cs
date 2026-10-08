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

    public Product(Guid productId, string name) : base(productId)
    {
        Name = name;
    }

    //Required for EF Core
    private Product() 
    {
        Name = "";
    }
}
