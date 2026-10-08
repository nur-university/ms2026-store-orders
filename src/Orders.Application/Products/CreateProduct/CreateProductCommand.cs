using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Application.Products.CreateProduct;

public class CreateProductCommand : IRequest<Guid>
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}
