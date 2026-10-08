using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nur.Store2026.Integration.Payments;
using Orders.Application.Orders.UseCases.CreateOrderSaga;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;

namespace Orders.Test.Application;

public class CreateOrderSagaHandlerTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IExternalPublisher> _publisher = new();
    private readonly CreateOrderSagaHandler _handler;

    public CreateOrderSagaHandlerTests()
    {
        _handler = new CreateOrderSagaHandler(_orders.Object, _publisher.Object, _unitOfWork.Object,
            NullLogger<CreateOrderSagaHandler>.Instance);
    }

    private Order OrderInStatus(OrderStatus status)
    {
        Order order = Order.Create([(Guid.NewGuid(), 10m, 2)]);
        if (status >= OrderStatus.Reserved && status <= OrderStatus.Paid) order.MarkAsReserved();
        if (status >= OrderStatus.PaymentInProcess && status <= OrderStatus.Paid) order.MarkAsPaymentInProcess();
        if (status == OrderStatus.Paid) order.MarkAsPaid();
        if (status == OrderStatus.Failed) order.MarkAsFailed("seed");
        if (status == OrderStatus.Canceled) order.MarkAsCanceled();
        order.ClearDomainEvents();

        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);
        return order;
    }

    private void VerifySaved(Times times) =>
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task PaymentCompleted_MarksOrderAsPaidAndSaves()
    {
        Order order = OrderInStatus(OrderStatus.PaymentInProcess);

        await _handler.HandleAsync(new PaymentCompleted(order.Id), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Single(order.DomainEvents.OfType<OrderPaid>());
        VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Failed)]
    [InlineData(OrderStatus.Canceled)]
    public async Task PaymentCompleted_ForOrderThatCannotBePaid_IsIgnored(OrderStatus status)
    {
        Order order = OrderInStatus(status);

        await _handler.HandleAsync(new PaymentCompleted(order.Id), CancellationToken.None);

        Assert.Equal(status, order.Status);
        Assert.Empty(order.DomainEvents);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task PaymentCompleted_BeforePaymentInProcess_ThrowsSoTheBrokerRedelivers()
    {
        Order order = OrderInStatus(OrderStatus.Reserved);

        await Assert.ThrowsAsync<DomainException>(
            () => _handler.HandleAsync(new PaymentCompleted(order.Id), CancellationToken.None));
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task PaymentCompleted_ForUnknownOrder_IsIgnored()
    {
        _orders.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), false)).ReturnsAsync((Order?)null);

        await _handler.HandleAsync(new PaymentCompleted(Guid.NewGuid()), CancellationToken.None);

        VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(OrderStatus.Reserved)]
    [InlineData(OrderStatus.PaymentInProcess)]
    public async Task PaymentFailed_FailsOrderAndRequestsStockRelease(OrderStatus status)
    {
        Order order = OrderInStatus(status);

        await _handler.HandleAsync(new PaymentFailed(order.Id, "declined"), CancellationToken.None);

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Contains("declined", order.FailureReason);
        Assert.Single(order.DomainEvents.OfType<OrderStockReleaseRequested>());
        VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Failed)]
    [InlineData(OrderStatus.Canceled)]
    public async Task PaymentFailed_ForOrderWithNothingToCompensate_IsIgnored(OrderStatus status)
    {
        Order order = OrderInStatus(status);

        await _handler.HandleAsync(new PaymentFailed(order.Id, "declined"), CancellationToken.None);

        Assert.Equal(status, order.Status);
        Assert.Empty(order.DomainEvents);
        VerifySaved(Times.Never());
    }
}
