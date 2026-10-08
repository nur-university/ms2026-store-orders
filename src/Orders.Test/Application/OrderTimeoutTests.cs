using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nur.Store2026.Integration.Inventory;
using Orders.Application.Orders.UseCases.CreateOrderSaga;
using Orders.Application.Orders.UseCases.ExpireOrder;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;
using Orders.Infrastructure.Timeouts;

namespace Orders.Test.Application;

public class OrderTimeoutTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static Order OrderInStatus(OrderStatus status)
    {
        Order order = Order.Create([(Guid.NewGuid(), 10m, 2)]);
        if (status >= OrderStatus.Reserved && status <= OrderStatus.Paid) order.MarkAsReserved();
        if (status >= OrderStatus.PaymentInProcess && status <= OrderStatus.Paid) order.MarkAsPaymentInProcess();
        if (status == OrderStatus.Paid) order.MarkAsPaid();
        if (status == OrderStatus.Failed) order.MarkAsFailed("seed");
        order.ClearDomainEvents();
        return order;
    }

    // ---- ExpireOrderHandler

    [Theory]
    [InlineData(OrderStatus.Started, false)]
    [InlineData(OrderStatus.Reserved, true)]
    [InlineData(OrderStatus.PaymentInProcess, true)]
    public async Task Expire_FailsTheOrder_AndReleasesStockOnlyIfItWasReserved(OrderStatus status, bool stockWasReserved)
    {
        Order order = OrderInStatus(status);
        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);
        ExpireOrderHandler handler = new(_orders.Object, _unitOfWork.Object);

        Result<bool> result = await handler.Handle(new ExpireOrderCommand(order.Id, status, "Timeout"), CancellationToken.None);

        Assert.True(result.Value);
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal("Timeout", order.FailureReason);
        Assert.Equal(stockWasReserved, order.DomainEvents.OfType<OrderStockReleaseRequested>().Any());
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Expire_DoesNothing_WhenTheOrderProgressedMeanwhile()
    {
        Order order = OrderInStatus(OrderStatus.Paid);
        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);
        ExpireOrderHandler handler = new(_orders.Object, _unitOfWork.Object);

        Result<bool> result = await handler.Handle(
            new ExpireOrderCommand(order.Id, OrderStatus.PaymentInProcess, "Timeout"), CancellationToken.None);

        Assert.False(result.Value);
        Assert.Equal(OrderStatus.Paid, order.Status);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- late InventoryReserved

    [Theory]
    [InlineData(OrderStatus.Failed)]
    public async Task InventoryReserved_ForAClosedOrder_RequestsStockRelease(OrderStatus status)
    {
        Order order = OrderInStatus(status);
        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);
        CreateOrderSagaHandler saga = new(_orders.Object, Mock.Of<IExternalPublisher>(), _unitOfWork.Object,
            NullLogger<CreateOrderSagaHandler>.Instance);

        await saga.HandleAsync(new InventoryReserved { SourceId = order.Id, SourceType = "orders" }, CancellationToken.None);

        Assert.Equal(status, order.Status);
        Assert.Single(order.DomainEvents.OfType<OrderStockReleaseRequested>());
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RequestStockRelease_OnAnOpenOrder_Throws()
    {
        Order order = OrderInStatus(OrderStatus.Reserved);

        Assert.Throws<DomainException>(() => order.RequestStockRelease("x"));
    }

    // ---- background service

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task BackgroundService_ExpiresEveryStaleOrder_AndKeepsGoingWhenOneFails()
    {
        Guid ok1 = Guid.NewGuid(), broken = Guid.NewGuid(), ok2 = Guid.NewGuid();
        DateTimeOffset now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        OrderTimeoutOptions options = new()
        {
            StartedTimeout = TimeSpan.FromMinutes(5),
            ReservedTimeout = TimeSpan.FromMinutes(6),
            PaymentTimeout = TimeSpan.FromMinutes(30)
        };

        _orders.Setup(r => r.GetStaleAsync(now.UtcDateTime.AddMinutes(-5), now.UtcDateTime.AddMinutes(-6), now.UtcDateTime.AddMinutes(-30), options.BatchSize))
            .ReturnsAsync([(ok1, OrderStatus.Started), (broken, OrderStatus.Reserved), (ok2, OrderStatus.PaymentInProcess)]);

        Mock<IMediator> mediator = new();
        mediator.Setup(m => m.Send(It.Is<ExpireOrderCommand>(c => c.OrderId == broken), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("concurrency conflict"));
        mediator.Setup(m => m.Send(It.Is<ExpireOrderCommand>(c => c.OrderId != broken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        ServiceCollection services = new();
        services.AddScoped(_ => _orders.Object);
        services.AddScoped(_ => mediator.Object);
        OrderTimeoutBackgroundService service = new(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options), new FixedClock(now), NullLogger<OrderTimeoutBackgroundService>.Instance);

        int expired = await service.ExpireStaleOrdersAsync(CancellationToken.None);

        Assert.Equal(2, expired);
        mediator.Verify(m => m.Send(It.Is<ExpireOrderCommand>(c => c.OrderId == ok1 && c.ExpectedStatus == OrderStatus.Started),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<ExpireOrderCommand>(c => c.OrderId == ok2 && c.ExpectedStatus == OrderStatus.PaymentInProcess),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
