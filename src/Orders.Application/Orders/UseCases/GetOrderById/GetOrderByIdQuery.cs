using MediatR;

namespace Orders.Application.Orders.UseCases.GetOrderById;

public record GetOrderByIdQuery : IRequest<Guid>
{
    public Guid OrderId { get; set; }
}
