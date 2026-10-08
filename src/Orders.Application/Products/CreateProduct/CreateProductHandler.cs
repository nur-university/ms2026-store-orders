using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using MediatR;
using Nur.Store2026.Integration.Catalog;
using Orders.Application.Abstractions;
using Orders.Domain.Products;

namespace Orders.Application.Products.CreateProduct;

public class CreateProductHandler(
    IProductRepository productRepository, 
    IUnitOfWork unitOfWork) : 
        IRequestHandler<CreateProductCommand, Guid>,
        IIntegrationMessageConsumer<ProductCreated>
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Product product = new(request.ProductId, request.Name);
        await _productRepository.AddAsync(product);

        await _unitOfWork.CommitAsync(cancellationToken);

        return product.Id;
    }

    public async Task HandleAsync(ProductCreated message, CancellationToken cancellationToken)
    {
        CreateProductCommand command = new()
        {
            Name = message.Name,
            ProductId = message.ProductId,
        };

        await Handle(command, cancellationToken);
    }
}
