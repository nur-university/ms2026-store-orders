using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.GetOrderById;

public record OrderDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public decimal Total { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime StartedOn { get; init; }
    public DateTime? ReservedOn { get; init; }
    public DateTime? ProcessingPaymentOn { get; init; }
    public DateTime? PaidOn { get; init; }
    public DateTime? CanceledOn { get; init; }
    public DateTime? CompletedOn { get; init; }
    public DateTime? FailedOn { get; init; }
    public string? FailureReason { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];

    public static OrderDto From(Order order) => new()
    {
        Id = order.Id,
        UserId = order.UserId,
        Total = order.TotalAmount,
        Status = order.Status.ToString(),
        StartedOn = order.StartedOn,
        ReservedOn = order.ReservedOn,
        ProcessingPaymentOn = order.ProcesingPaymentOn,
        PaidOn = order.PaidOn,
        CanceledOn = order.CanceledOn,
        CompletedOn = order.CompletedOn,
        FailedOn = order.FailedOn,
        FailureReason = order.FailureReason,
        Items = order.Items.Select(i => new OrderItemDto(i.ProductId, i.Price, i.Quantity)).ToList()
    };
}

public record OrderItemDto(Guid ProductId, decimal Price, int Quantity);
