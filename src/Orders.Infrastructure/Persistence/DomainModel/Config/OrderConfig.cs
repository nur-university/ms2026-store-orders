using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orders.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Infrastructure.Persistence.DomainModel.Config;

internal class OrderConfig : IEntityTypeConfiguration<Order>,
    IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("order");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("orderId");


        builder.Property(x => x.StartedOn)
            .HasColumnName("startedOn");

        var priceConverter = new ValueConverter<PriceValue, decimal>(
                valueObject => valueObject.Value, // PriceValue to decimal
                decimalValue => new PriceValue(decimalValue) // decimal to PriceValue
            );

        builder.Property(x => x.TotalAmount)
            .HasConversion(priceConverter)
            .HasColumnName("totalAmount");

        var statusConverter = new ValueConverter<OrderStatus, string>(
                status => status.ToString(), // OrderStatus to string
                statusString => (OrderStatus)Enum.Parse(typeof(OrderStatus), statusString) // string to OrderStatus
            );

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter);
        
        builder.Property(x => x.ReservedOn)
            .HasColumnName("reservedOn");

        builder.Property(x => x.ProcesingPaymentOn)
            .HasColumnName("procesingPaymentOn");

        builder.Property(x => x.PaidOn)
            .HasColumnName("paidOn");
        ;
        builder.Property(x => x.CanceledOn)
            .HasColumnName("canceledOn");

        builder.Property(x => x.CompletedOn)
            .HasColumnName("completedOn");

        builder.Property(x => x.FailedOn)
            .HasColumnName("failedOn");

        builder.Property(x => x.FailureReason)
            .HasColumnName("failureReason")
            .HasMaxLength(1000);


        builder.HasMany(typeof(OrderItem), "_items");

        builder.Ignore("_domainEvents");
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.Items);
    }

    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("orderItem");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("orderItemId");

        builder.Property(x => x.ProductId)
            .HasColumnName("productId");

        var quantityConverter = new ValueConverter<QuantityValue, int>(
                quantityValue => quantityValue.Value,
                quantity => new QuantityValue(quantity)
            );

        builder.Property(x => x.Quantity)
            .HasConversion(quantityConverter)
            .HasColumnName("quantity");

        var priceConverter = new ValueConverter<PriceValue, decimal>(
                valueObject => valueObject.Value, // PriceValue to decimal
                decimalValue => new PriceValue(decimalValue) // decimal to PriceValue
            );

        builder.Property(x => x.Price)
            .HasConversion(priceConverter)
            .HasColumnName("price");

        builder.Property(x => x.OrderId)
            .HasColumnName("orderId");
    }
}
