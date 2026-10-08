using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;

namespace Orders.Test.Domain;

public class OrderTests
{
    private static Order NewOrder() => Order.Create([(Guid.NewGuid(), 10m, 2)]);

    private static Order InStatus(OrderStatus status)
    {
        Order order = NewOrder();
        if (status >= OrderStatus.Reserved && status <= OrderStatus.Paid) order.MarkAsReserved();
        if (status >= OrderStatus.PaymentInProcess && status <= OrderStatus.Paid) order.MarkAsPaymentInProcess();
        if (status == OrderStatus.Paid) order.MarkAsPaid();
        order.ClearDomainEvents();
        return order;
    }

    [Fact]
    public void Create_RaisesOrderStarted()
    {
        Order order = NewOrder();

        Assert.Equal(OrderStatus.Started, order.Status);
        Assert.Single(order.DomainEvents.OfType<OrderStarted>());
    }

    [Fact]
    public void MarkAsPaid_FromPaymentInProcess_RaisesOrderPaid()
    {
        Order order = InStatus(OrderStatus.PaymentInProcess);

        order.MarkAsPaid();

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Single(order.DomainEvents.OfType<OrderPaid>());
    }

    [Fact]
    public void MarkAsPaid_FromReserved_Throws()
    {
        Order order = InStatus(OrderStatus.Reserved);

        Assert.Throws<DomainException>(() => order.MarkAsPaid());
        Assert.Equal(OrderStatus.Reserved, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Reserved)]
    [InlineData(OrderStatus.PaymentInProcess)]
    public void MarkAsFailed_WithReservedStock_RequestsStockRelease(OrderStatus from)
    {
        Order order = InStatus(from);

        order.MarkAsFailed("card declined");

        Assert.Equal(OrderStatus.Failed, order.Status);
        OrderStockReleaseRequested release = Assert.Single(order.DomainEvents.OfType<OrderStockReleaseRequested>());
        Assert.Equal(order.Id, release.OrderId);
        Assert.Equal("card declined", release.Reason);
    }

    [Fact]
    public void MarkAsFailed_BeforeReservation_DoesNotRequestStockRelease()
    {
        Order order = InStatus(OrderStatus.Started);

        order.MarkAsFailed("no stock");

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Empty(order.DomainEvents.OfType<OrderStockReleaseRequested>());
    }

    [Theory]
    [InlineData(OrderStatus.Reserved, true)]
    [InlineData(OrderStatus.PaymentInProcess, true)]
    [InlineData(OrderStatus.Started, false)]
    public void MarkAsCanceled_RequestsCompensationOnlyWhileStockIsReserved(OrderStatus from, bool expectsCompensation)
    {
        Order order = InStatus(from);

        order.MarkAsCanceled();

        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal(expectsCompensation, order.DomainEvents.OfType<OrderStockReleaseRequested>().Any());
        // A payment may already be pending exactly when stock is reserved.
        Assert.Equal(expectsCompensation, order.DomainEvents.OfType<OrderPaymentCancellationRequested>().Any());
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Failed)]
    public void MarkAsCanceled_AfterPaymentOrFailure_IsRejected(OrderStatus from)
    {
        Order order = InStatus(from == OrderStatus.Failed ? OrderStatus.Started : from);
        if (from == OrderStatus.Failed) order.MarkAsFailed("seed");

        DomainException ex = Assert.Throws<DomainException>(() => order.MarkAsCanceled());

        Assert.Equal("Order.CanceledNotAllowed", ex.Error.Code);
        Assert.Equal(from, order.Status);
    }

    [Fact]
    public void MarkAsFailed_WithPendingPayment_RequestsPaymentCancellation()
    {
        Order order = InStatus(OrderStatus.PaymentInProcess);

        order.MarkAsFailed("card declined");

        OrderPaymentCancellationRequested cancellation = Assert.Single(order.DomainEvents.OfType<OrderPaymentCancellationRequested>());
        Assert.Equal("card declined", cancellation.Reason);
    }

    [Fact]
    public void RequestPaymentCancellation_OnAnOpenOrder_Throws()
    {
        Order order = InStatus(OrderStatus.Reserved);

        Assert.Throws<DomainException>(() => order.RequestPaymentCancellation("x"));
    }

    [Fact]
    public void Create_KeepsTheCustomerOfTheSalesNote_AndPassesItOnWhenReserved()
    {
        Order order = Order.Create([(Guid.NewGuid(), 10m, 2)], Guid.NewGuid(), "  Ana Perez ", "1234567");
        order.ClearDomainEvents();

        order.MarkAsReserved();

        Assert.Equal("Ana Perez", order.CustomerName);
        OrderReserved reserved = Assert.Single(order.DomainEvents.OfType<OrderReserved>());
        Assert.Equal("Ana Perez", reserved.CustomerName);
        Assert.Equal("1234567", reserved.CustomerDocument);
    }

    [Fact]
    public void Create_TreatsBlankCustomerDataAsMissing()
    {
        Order order = Order.Create([(Guid.NewGuid(), 10m, 2)], Guid.NewGuid(), " ", "");

        Assert.Null(order.CustomerName);
        Assert.Null(order.CustomerDocument);
    }

    [Fact]
    public void MarkAsPaid_StoresTheInvoice_AndOnlyAPaidOrderCanBeCompleted()
    {
        Order order = InStatus(OrderStatus.PaymentInProcess);
        Assert.Throws<DomainException>(() => order.MarkAsCompleted());

        order.MarkAsPaid("F-00000042");
        order.MarkAsCompleted();

        Assert.Equal("F-00000042", order.InvoiceNumber);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedOn);
    }

    [Fact]
    public void Create_KeepsTheUser_AndPublishesItInOrderStarted()
    {
        Guid userId = Guid.NewGuid();

        Order order = Order.Create([(Guid.NewGuid(), 10m, 2)], userId);

        Assert.Equal(userId, order.UserId);
        Assert.Equal(userId, Assert.Single(order.DomainEvents.OfType<OrderStarted>()).UserId);
    }
}
