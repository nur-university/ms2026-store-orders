namespace Orders.Application.Orders.UseCases.GetOrderById;

public record OrderDto
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } 

}
