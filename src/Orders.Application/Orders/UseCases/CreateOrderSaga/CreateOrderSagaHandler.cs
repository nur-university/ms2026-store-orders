using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Joseco.Outbox.Contracts.Model;
using MediatR;
using Nur.Store2026.Integration.Inventory;
using Nur.Store2026.Integration.Orders;
using Nur.Store2026.Integration.Payments;
using Orders.Application.Orders.CreateOrder;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.CreateOrderSaga;

public class CreateOrderSagaHandler :
    IRequestHandler<CreateOrderCommand, Result<Guid>>,
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderStarted>>,
    IIntegrationMessageConsumer<InventoryReserved>,
    IIntegrationMessageConsumer<InventoryReservationFailed>,
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderReserved>>,
    IIntegrationMessageConsumer<OrderPaymentInProcessRegistered>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExternalPublisher _publisher;

    public CreateOrderSagaHandler(IOrderRepository orderRepository, IExternalPublisher publisher, IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Creates a new order and saves it to the database.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        List<(Guid productId, decimal price, int quantity)> items = [.. request.Items.Select(i => (i.ProductId, i.Price, i.Quantity))];

        Order order = Order.Create(items);

        await _orderRepository.AddAsync(order);
        await _unitOfWork.CommitAsync(cancellationToken);
        return order.Id;
    }

    /// <summary>
    /// Handles the OrderCreated event from OutboxProcessor and publishes an integration message to the external system.
    /// </summary>
    /// <param name="notification"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task Handle(OutboxMessage<Domain.Orders.Events.OrderStarted> notification, CancellationToken cancellationToken)
    {
        OrderItemStarted[] items =
            [
                .. notification.Content.Items
                .Select(i => new OrderItemStarted()
                {
                    ProductId = i.ProductId,
                    Price = i.Price,
                    Quantity = i.Quantity
                })
            ];

        Nur.Store2026.Integration.Orders.OrderStarted integrationMessage =
            new(notification.Content.OrderId, items);

        await _publisher.PublishAsync(integrationMessage);
    }

    /// <summary>
    /// Handles the InventoryReserved integration message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task HandleAsync(InventoryReserved message, CancellationToken cancellationToken)
    {
        if (message.SourceType != "orders")
        {
            return;
        }

        Order? order = await _orderRepository.GetByIdAsync(message.SourceId);
        if (order == null)
        {
            return;
        }

        order.MarkAsReserved();

        await _unitOfWork.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Handles the OrderReserved event from OutboxProcessor and publishes an integration message to the external system.
    /// </summary>
    /// <param name="notification"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task Handle(OutboxMessage<Domain.Orders.Events.OrderReserved> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.Content;
        Nur.Store2026.Integration.Orders.OrderReserved integrationMessage = new(domainEvent.OrderId, domainEvent.TotalAmount);

        return _publisher.PublishAsync(integrationMessage);
    }

    public async Task HandleAsync(OrderPaymentInProcessRegistered message, CancellationToken cancellationToken)
    {
        Order? order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            return;
        }
        order.MarkAsPaymentInProcess();

        await _unitOfWork.CommitAsync(cancellationToken);

    }

    public async Task HandleAsync(InventoryReservationFailed message, CancellationToken cancellationToken)
    {
        Order? order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            return;
        }
        order.MarkAsFailed(message.Reason);

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
