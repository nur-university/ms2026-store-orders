using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orders.Infrastructure.Persistence.StoredModel.Entities;

[Table("orderItem")]
internal class OrderItemStoredModel 
{
    [Key]
    [Column("orderItemId")]

    public Guid Id { get; set; }
    [Column("orderId")]
    public Guid OrderId { get; set; }
    public OrderStoredModel Order { get; set; } = null!;

    [Required]
    [Column("productId")]
    public Guid ProductId { get; set; }
    public ProductStoredModel Product { get; set; } = null!;

    [Column("productName")]
    [MaxLength(250)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Column("quantity")]
    public int Quantity { get; set; }

    [Required]
    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
}
