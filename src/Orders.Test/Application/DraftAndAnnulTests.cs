using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Moq;
using Orders.Application.Orders;
using Orders.Application.Orders.CreateOrder;
using Orders.Application.Orders.UseCases.AnnulOrder;
using Orders.Application.Orders.UseCases.Drafts;
using Orders.Application.Orders.UseCases.ExpireOrder;
using Orders.Application.Orders.UseCases.GetOrders;
using Orders.Domain.Orders;
using Orders.Domain.Orders.Events;
using Orders.Domain.Products;

namespace Orders.Test.Application;

/// <summary>The sales note written in several steps (draft), and the manual annulment of a paid sale nobody picked up.</summary>
public class DraftAndAnnulTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _advisor = Guid.NewGuid();
    private readonly Product _tv = new(Guid.NewGuid(), "Smart TV", 6000m);
    private readonly Product _fridge = new(Guid.NewGuid(), "Fridge", 4000m);

    public DraftAndAnnulTests()
    {
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((IEnumerable<Guid> ids) => new[] { _tv, _fridge }.Where(p => ids.Contains(p.Id)).ToList());
    }

    private OrderLinesPricer Pricer() => new(_products.Object);

    private void VerifySaved(Times times) =>
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), times);

    private Order StoredDraft(Guid? owner = null)
    {
        Order draft = Order.CreateDraft(owner ?? _advisor, "Ana Perez", "1234567");
        _orders.Setup(r => r.GetByIdAsync(draft.Id, false)).ReturnsAsync(draft);
        return draft;
    }

    // ---- domain

    [Fact]
    public void Draft_HoldsLinesButStartsNothing()
    {
        Order draft = Order.CreateDraft(_advisor);

        draft.SetDraftLines([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 2)]);

        Assert.Equal(OrderStatus.Draft, draft.Status);
        Assert.Equal(12000m, (decimal)draft.TotalAmount);
        Assert.Empty(draft.DomainEvents); // no stock reservation, no charge order
    }

    [Fact]
    public void Confirm_PricesTheLinesAgain_StartsTheSale_AndRestartsTheClock()
    {
        Order draft = Order.CreateDraft(_advisor);
        draft.SetDraftLines([new OrderLineInput(_tv.Id, "Smart TV", 5000m, 1)]);
        DateTime openedOn = draft.StartedOn;

        draft.Confirm([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)]);

        Assert.Equal(OrderStatus.Started, draft.Status);
        Assert.Equal(6000m, (decimal)draft.TotalAmount);
        Assert.True(draft.StartedOn >= openedOn);
        OrderStarted started = Assert.Single(draft.DomainEvents.OfType<OrderStarted>());
        Assert.Equal(_advisor, started.UserId);
        Assert.Equal(6000m, Assert.Single(started.Items).Price);
    }

    [Fact]
    public void Confirm_AnEmptyDraft_IsRejected()
    {
        Order draft = Order.CreateDraft(_advisor);

        Assert.Throws<DomainException>(() => draft.Confirm([]));
        Assert.Equal(OrderStatus.Draft, draft.Status);
    }

    [Fact]
    public void ADraftThatWasConfirmedOrDiscarded_CannotBeEditedAgain()
    {
        Order confirmed = Order.CreateDraft(_advisor);
        confirmed.Confirm([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)]);
        Order discarded = Order.CreateDraft(_advisor);
        discarded.Discard();

        Assert.Throws<DomainException>(() => confirmed.SetDraftLines([]));
        Assert.Throws<DomainException>(() => confirmed.Discard());
        Assert.Throws<DomainException>(() => discarded.Confirm([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)]));
    }

    [Fact]
    public void ADraftCannotBeCanceledAsASale()
    {
        Order draft = Order.CreateDraft(_advisor);

        DomainException ex = Assert.Throws<DomainException>(() => draft.MarkAsCanceled());

        Assert.Equal("Order.CanceledNotAllowed", ex.Error.Code);
    }

    [Fact]
    public void Reserved_CarriesTheLinesWithTheNameAndPriceOfTheSale()
    {
        Order order = Order.Create([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 2)], _advisor);
        order.ClearDomainEvents();

        order.MarkAsReserved();

        OrderReservedLine line = Assert.Single(Assert.Single(order.DomainEvents.OfType<OrderReserved>()).Lines!);
        Assert.Equal("Smart TV", line.ProductName);
        Assert.Equal(6000m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void Annul_AReleasedSale_ReleasesTheStockAndFlagsTheRefund()
    {
        Order order = Order.Create([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)], _advisor);
        order.MarkAsReserved();
        order.MarkAsPaymentInProcess();
        order.MarkAsPaid("F-00000001");
        order.ClearDomainEvents();

        order.Annul("customer never came back");

        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.True(order.RefundRequired);
        Assert.Equal("customer never came back", order.CancellationReason);
        Assert.Single(order.DomainEvents.OfType<OrderStockReleaseRequested>());
    }

    [Theory]
    [InlineData(OrderStatus.Started)]
    [InlineData(OrderStatus.PaymentInProcess)]
    [InlineData(OrderStatus.Completed)]
    public void Annul_IsOnlyForPaidSalesNotYetPickedUp(OrderStatus status)
    {
        Order order = Order.Create([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)], _advisor);
        if (status >= OrderStatus.Reserved) order.MarkAsReserved();
        if (status >= OrderStatus.PaymentInProcess) order.MarkAsPaymentInProcess();
        if (status == OrderStatus.Completed) { order.MarkAsPaid(); order.MarkAsCompleted(); }

        DomainException ex = Assert.Throws<DomainException>(() => order.Annul("reason"));

        Assert.Equal("Order.AnnulNotAllowed", ex.Error.Code);
        Assert.False(order.RefundRequired);
    }

    // ---- use cases

    [Fact]
    public async Task CreateDraft_PricesTheLinesAsAnEstimate_AndSavesWithoutStartingTheSale()
    {
        Order? stored = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>())).Callback<Order>(o => stored = o).Returns(Task.CompletedTask);

        Result<Guid> result = await new CreateDraftHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new CreateDraftCommand(_advisor, [new CreateOrderItem(_tv.Id, 1)], "Ana Perez"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Draft, stored!.Status);
        Assert.Equal(6000m, (decimal)stored.TotalAmount);
        Assert.Empty(stored.DomainEvents.OfType<OrderStarted>());
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task CreateDraft_MayStartEmpty()
    {
        Result<Guid> result = await new CreateDraftHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new CreateDraftCommand(_advisor), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateDraftItems_ReplacesTheLines()
    {
        Order draft = StoredDraft();

        Result<Guid> result = await new UpdateDraftItemsHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new UpdateDraftItemsCommand(draft.Id, _advisor, [new CreateOrderItem(_tv.Id, 1), new CreateOrderItem(_fridge.Id, 2)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, draft.Items.Count);
        Assert.Equal(14000m, (decimal)draft.TotalAmount);
    }

    [Fact]
    public async Task ADraftOfAnotherAdvisor_LooksLikeItDoesNotExist()
    {
        Order draft = StoredDraft(owner: Guid.NewGuid());
        Guid stranger = _advisor;

        Result<Guid> edit = await new UpdateDraftItemsHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new UpdateDraftItemsCommand(draft.Id, stranger, [new CreateOrderItem(_tv.Id, 1)]), CancellationToken.None);
        Result<Guid> confirm = await new ConfirmDraftHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new ConfirmDraftCommand(draft.Id, stranger), CancellationToken.None);
        Result<Guid> discard = await new DiscardDraftHandler(_orders.Object, _unitOfWork.Object).Handle(
            new DiscardDraftCommand(draft.Id, stranger), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, edit.Error.Type);
        Assert.Equal(ErrorType.NotFound, confirm.Error.Type);
        Assert.Equal(ErrorType.NotFound, discard.Error.Type);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task ConfirmDraft_UsesTheCurrentPriceOfEachProduct()
    {
        Order draft = StoredDraft();
        draft.SetDraftLines([new OrderLineInput(_tv.Id, "Smart TV", 1m, 1)]); // a stale estimate
        draft.ClearDomainEvents();

        Result<Guid> result = await new ConfirmDraftHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new ConfirmDraftCommand(draft.Id, _advisor), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Started, draft.Status);
        Assert.Equal(6000m, (decimal)draft.TotalAmount);
        Assert.Single(draft.DomainEvents.OfType<OrderStarted>());
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task ConfirmDraft_WithoutLines_IsRejected()
    {
        Order draft = StoredDraft();

        Result<Guid> result = await new ConfirmDraftHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new ConfirmDraftCommand(draft.Id, _advisor), CancellationToken.None);

        Assert.Equal("Order.Empty", result.Error.Code);
        Assert.Equal(OrderStatus.Draft, draft.Status);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task ConfirmDraft_ThatIsAlreadyStarted_IsAConflict()
    {
        Order order = Order.Create([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)], _advisor);
        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);

        Result<Guid> result = await new ConfirmDraftHandler(_orders.Object, Pricer(), _unitOfWork.Object).Handle(
            new ConfirmDraftCommand(order.Id, _advisor), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task ExpireDraft_DiscardsOnlyWhatWasNotTouchedSince()
    {
        Order draft = StoredDraft();
        ExpireDraftHandler handler = new(_orders.Object, _unitOfWork.Object);

        Result<bool> touchedRecently = await handler.Handle(
            new ExpireDraftCommand(draft.Id, draft.UpdatedOn.AddMinutes(-1)), CancellationToken.None);
        Result<bool> stale = await handler.Handle(
            new ExpireDraftCommand(draft.Id, draft.UpdatedOn.AddMinutes(1)), CancellationToken.None);

        Assert.False(touchedRecently.Value);
        Assert.True(stale.Value);
        Assert.Equal(OrderStatus.Discarded, draft.Status);
    }

    [Fact]
    public async Task AnnulOrder_RequiresAReason_AndAPaidSale()
    {
        Order order = Order.Create([new OrderLineInput(_tv.Id, "Smart TV", 6000m, 1)], _advisor);
        order.MarkAsReserved();
        order.MarkAsPaymentInProcess();
        order.MarkAsPaid("F-1");
        _orders.Setup(r => r.GetByIdAsync(order.Id, false)).ReturnsAsync(order);
        AnnulOrderHandler handler = new(_orders.Object, _unitOfWork.Object);

        Result<Guid> noReason = await handler.Handle(new AnnulOrderCommand(order.Id, "  "), CancellationToken.None);
        Result<Guid> annulled = await handler.Handle(new AnnulOrderCommand(order.Id, "abandoned"), CancellationToken.None);
        Result<Guid> again = await handler.Handle(new AnnulOrderCommand(order.Id, "abandoned"), CancellationToken.None);

        Assert.Equal("Order.AnnulReasonRequired", noReason.Error.Code);
        Assert.True(annulled.IsSuccess);
        Assert.Equal(ErrorType.Conflict, again.Error.Type);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task ListPaidSales_FiltersByStatusAndAge()
    {
        _orders.Setup(r => r.GetPageAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderStatus?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(([], 0));

        await new GetOrdersHandler(_orders.Object).Handle(
            new GetOrdersQuery(_advisor, true, 1, 20, OrderStatus.Paid, 7), CancellationToken.None);

        _orders.Verify(r => r.GetPageAsync(null, 0, 20, OrderStatus.Paid,
            It.Is<DateTime?>(d => d.HasValue && Math.Abs((DateTime.UtcNow.AddDays(-7) - d.Value).TotalMinutes) < 1)), Times.Once);
    }
}
