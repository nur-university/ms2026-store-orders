
using Joseco.Communication.External.Contracts.Services;
using Joseco.Communication.External.RabbitMQ.Services;
using Joseco.Communication.External.RabbitMQ;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nur.Store2026.Integration.Catalog;
using Nur.Store2026.Integration.Identity;
using Nur.Store2026.Integration.Inventory;
using Nur.Store2026.Integration.Payments;
using Orders.Application.Orders.UseCases.CreateOrderSaga;
using Orders.Application.Products.CreateProduct;
using Orders.Infrastructure.Observability;

namespace Orders.Infrastructure.Extensions;

public static class BrokerExtensions
{
    public static IServiceCollection AddRabbitMQ(this IServiceCollection services, IHostEnvironment environment)
    {
        using var serviceProvider = services.BuildServiceProvider();
        var rabbitMqSettings = serviceProvider.GetRequiredService<RabbitMqSettings>();

        bool isWebApp = environment is IWebHostEnvironment;
        services.AddRabbitMQ(rabbitMqSettings);

        if (isWebApp)
        {
            return services;
        }

        services.AddRabbitMqConsumer<ProductCreated, CreateProductHandler>("orders-product-created");

        services.AddRabbitMqConsumer<InventoryReserved, CreateOrderSagaHandler>("orders-inventory-reserved");
        services.AddRabbitMqConsumer<OrderPaymentInProcessRegistered, CreateOrderSagaHandler>("orders-order-payment-in-process-registered");
        services.AddRabbitMqConsumer<InventoryReservationFailed, CreateOrderSagaHandler>("orders-inventory-reservation-failed");
        services.AddRabbitMqConsumer<PaymentCompleted, CreateOrderSagaHandler>("orders-payment-completed");
        services.AddRabbitMqConsumer<PaymentFailed, CreateOrderSagaHandler>("orders-payment-failed");

        services.Decorate<IIntegrationMessageConsumer<ProductCreated>, TracingConsumer<ProductCreated>>();
        services.Decorate<IIntegrationMessageConsumer<InventoryReserved>, TracingConsumer<InventoryReserved>>();
        services.Decorate<IIntegrationMessageConsumer<OrderPaymentInProcessRegistered>, TracingConsumer<OrderPaymentInProcessRegistered>>();
        services.Decorate<IIntegrationMessageConsumer<InventoryReservationFailed>, TracingConsumer<InventoryReservationFailed>>();
        services.Decorate<IIntegrationMessageConsumer<PaymentCompleted>, TracingConsumer<PaymentCompleted>>();
        services.Decorate<IIntegrationMessageConsumer<PaymentFailed>, TracingConsumer<PaymentFailed>>();



        return services;
    }
}
