using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Infrastructure.Persistence.StoredModel.Entities;

[Table("product")]
internal class ProductStoredModel
{
    [Key]
    [Column("productId")]
    public Guid Id { get; set; }

    [Column("name")]
    [StringLength(250)]
    [Required]
    public string Name { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal? Price { get; set; }
}
