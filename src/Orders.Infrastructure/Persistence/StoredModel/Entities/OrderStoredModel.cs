using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Infrastructure.Persistence.StoredModel.Entities;

[Table("order")]
internal class OrderStoredModel
{
    [Key]
    [Column("orderId")]
    public Guid Id { get; set; }

    [Column("startedOn")]
    [Required]
    public DateTime StartedOn { get; set; }
        
    [Required]
    [Column("totalAmount", TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column("status")]
    [MaxLength(25)]
    [Required]
    public string Status { get; set; } = string.Empty;

    [Column("reservedOn")]
    public DateTime? ReservedOn { get; set; }

    [Column("procesingPaymentOn")]
    public DateTime? ProcesingPaymentOn { get; set; }

    [Column("paidOn")]
    public DateTime? PaidOn { get; set; }

    [Column("canceledOn")]
    public DateTime? CanceledOn { get; set; }

    [Column("completedOn")]
    public DateTime? CompletedOn { get; set; }

    [Column("failedOn")]
    public DateTime? FailedOn { get; set; }

    [Column("userId")]
    public Guid? UserId { get; set; }

    [Column("customerName")]
    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [Column("customerDocument")]
    [MaxLength(50)]
    public string? CustomerDocument { get; set; }

    [Column("invoiceNumber")]
    [MaxLength(20)]
    public string? InvoiceNumber { get; set; }

    [Column("updatedOn")]
    public DateTime UpdatedOn { get; set; }

    [Column("cancellationReason")]
    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    [Column("refundRequired")]
    public bool RefundRequired { get; set; }

    [Column("failureReason")]
    [MaxLength(1000)]
    public string? FailureReason { get; set; } = string.Empty;
}
