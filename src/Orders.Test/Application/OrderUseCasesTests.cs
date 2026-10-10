using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nur.Store2026.Integration.Payments;
using Orders.Application.Orders;
using Orders.Application.Orders.CreateOrder;
using Orders.Application.Orders.UseCases.CancelOrder;
using Orders.Application.Orders.UseCases.CreateOrderSaga;
using Orders.Application.Orders.UseCases.GetOrderById;
using Orders.Application.Orders.UseCases.GetOrders;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;
using Orders.Domain.Products;

namespace Orders.Test.Application;

public class OrderUseCasesTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _user = Guid.NewGuid();

    private void VerifySaved(Times times) =>
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), times);

    private Order OwnedOrder(Guid owner, OrderStatus status = OrderStatus.Started)
    {
        Order order = Order.Create([(Guid.NewGuid(), 10m, 2)], owner);
        if (status >= OrderStatus.Reserved) order.MarkAsReserved();
        if (status >= OrderStatus.PaymentInProcess) order.MarkAsPaymentInProcess();
        if (status == OrderStatus.Paid) { order.MarkAsPaid(); }
        order.ClearDomainEvents();
        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);
        _orders.Setup(r => r.GetByIdAsync(order.Id, true)).ReturnsAsync(order);
        return order;
    }

    // ---- create

    private CreateOrderHandler CreateHandler() => new(_orders.Object, new OrderLinesPricer(_products.Object), _unitOfWork.Object);

    private void KnownProducts(params Product[] products) =>
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(products);

    [Fact]
    public async Task Create_PricesTheItemsFromTheProductsAndStoresTheUser()
    {
        Product tv = new(Guid.NewGuid(), "TV", 4000m);
        KnownProducts(tv);
        Order? stored = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>())).Callback<Order>(o => stored = o).Returns(Task.CompletedTask);

        Result<Guid> result = await CreateHandler().Handle(
            new CreateOrderCommand(_user, [new CreateOrderItem(tv.Id, 2)]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stored);
        Assert.Equal(_user, stored!.UserId);
        Assert.Equal(8000m, (decimal)stored.TotalAmount);
        Assert.Equal(4000m, (decimal)Assert.Single(stored.Items).Price);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Create_MergesTheSameProductListedTwice()
    {
        Product tv = new(Guid.NewGuid(), "TV", 100m);
        KnownProducts(tv);
        Order? stored = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>())).Callback<Order>(o => stored = o).Returns(Task.CompletedTask);

        await CreateHandler().Handle(
            new CreateOrderCommand(_user, [new CreateOrderItem(tv.Id, 1), new CreateOrderItem(tv.Id, 2)]), CancellationToken.None);

        OrderItem line = Assert.Single(stored!.Items);
        Assert.Equal(3, (int)line.Quantity);
    }

    [Fact]
    public async Task Create_RejectsAnUnknownProduct()
    {
        KnownProducts();
        Guid unknown = Guid.NewGuid();

        Result<Guid> result = await CreateHandler().Handle(
            new CreateOrderCommand(_user, [new CreateOrderItem(unknown, 1)]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Order.ProductNotFound", result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Create_RejectsAProductWithoutAPrice(int? price)
    {
        Product product = new(Guid.NewGuid(), "No price", price.HasValue ? price.Value : null);
        KnownProducts(product);

        Result<Guid> result = await CreateHandler().Handle(
            new CreateOrderCommand(_user, [new CreateOrderItem(product.Id, 1)]), CancellationToken.None);

        Assert.Equal("Order.ProductPriceUnavailable", result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Create_RejectsAnEmptyOrderAndNonPositiveQuantities()
    {
        Result<Guid> empty = await CreateHandler().Handle(new CreateOrderCommand(_user, []), CancellationToken.None);
        Result<Guid> zero = await CreateHandler().Handle(
            new CreateOrderCommand(_user, [new CreateOrderItem(Guid.NewGuid(), 0)]), CancellationToken.None);

        Assert.Equal("Order.Empty", empty.Error.Code);
        Assert.True(zero.IsFailure);
        VerifySaved(Times.Never());
    }

    // ---- read

    [Fact]
    public async Task GetById_ReturnsTheOrderToItsOwner_AndHidesItFromOthers()
    {
        Order order = OwnedOrder(_user);
        GetOrderByIdHandler handler = new(_orders.Object);

        Result<OrderDto> mine = await handler.Handle(new GetOrderByIdQuery(order.Id, _user, false), CancellationToken.None);
        Result<OrderDto> stranger = await handler.Handle(new GetOrderByIdQuery(order.Id, Guid.NewGuid(), false), CancellationToken.None);
        Result<OrderDto> staff = await handler.Handle(new GetOrderByIdQuery(order.Id, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(mine.IsSuccess);
        Assert.Equal("Started", mine.Value.Status);
        Assert.Equal(20m, mine.Value.Total);
        Assert.Equal(ErrorType.NotFound, stranger.Error.Type); // not 403: do not reveal that it exists
        Assert.True(staff.IsSuccess);
    }

    [Fact]
    public async Task GetOrders_FiltersByUser_ExceptForStaff()
    {
        _orders.Setup(r => r.GetPageAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderStatus?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(([], 0));
        GetOrdersHandler handler = new(_orders.Object);

        await handler.Handle(new GetOrdersQuery(_user, false, 3, 10), CancellationToken.None);
        await handler.Handle(new GetOrdersQuery(_user, true, 1, 1000), CancellationToken.None);

        _orders.Verify(r => r.GetPageAsync(_user, 20, 10, null, null), Times.Once);   // page 3 of 10 -> skip 20
        _orders.Verify(r => r.GetPageAsync(null, 0, 100, null, null), Times.Once);    // staff sees all; page size capped at 100
    }

    // ---- cancel

    [Fact]
    public async Task Cancel_ByTheOwner_CancelsAndCompensates()
    {
        Order order = OwnedOrder(_user, OrderStatus.PaymentInProcess);

        Result<Guid> result = await new CancelOrderHandler(_orders.Object, _unitOfWork.Object)
            .Handle(new CancelOrderCommand(order.Id, _user, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Single(order.DomainEvents.OfType<OrderStockReleaseRequested>());
        Assert.Single(order.DomainEvents.OfType<OrderPaymentCancellationRequested>());
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Cancel_ByAStranger_LooksLikeNotFound()
    {
        Order order = OwnedOrder(_user);

        Result<Guid> result = await new CancelOrderHandler(_orders.Object, _unitOfWork.Object)
            .Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), false), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(OrderStatus.Started, order.Status);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Cancel_APaidOrder_IsAConflict()
    {
        Order order = OwnedOrder(_user, OrderStatus.Paid);

        Result<Guid> result = await new CancelOrderHandler(_orders.Object, _unitOfWork.Object)
            .Handle(new CancelOrderCommand(order.Id, _user, false), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(OrderStatus.Paid, order.Status);
        VerifySaved(Times.Never());
    }

    // ---- late payment registration on a closed order

    [Fact]
    public async Task PaymentRegistered_ForAClosedOrder_RequestsPaymentCancellation()
    {
        Order order = OwnedOrder(_user);
        order.MarkAsCanceled();
        order.ClearDomainEvents();
        CreateOrderSagaHandler saga = new(_orders.Object, Mock.Of<IExternalPublisher>(), _unitOfWork.Object,
            NullLogger<CreateOrderSagaHandler>.Instance);

        await saga.HandleAsync(new OrderPaymentInProcessRegistered(order.Id), CancellationToken.None);

        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Single(order.DomainEvents.OfType<OrderPaymentCancellationRequested>());
        VerifySaved(Times.Once());
    }
}
