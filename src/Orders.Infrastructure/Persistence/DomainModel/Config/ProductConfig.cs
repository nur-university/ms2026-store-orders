using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Products;

namespace Orders.Infrastructure.Persistence.DomainModel.Config;

internal class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("productId");

        builder.Property(x => x.Name)
            .HasColumnName("name");

        builder.Ignore("_domainEvents");
        builder.Ignore(x => x.DomainEvents);
    }
}
