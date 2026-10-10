using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Joseco.Outbox.Contracts.Model;
using MediatR;
using Microsoft.Extensions.Logging;
using Nur.Store2026.Integration.Inventory;
using Nur.Store2026.Integration.Orders;
using Nur.Store2026.Integration.Payments;
using Orders.Application.Orders.CreateOrder;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.UseCases.CreateOrderSaga;

public class CreateOrderSagaHandler :
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderStarted>>,
    IIntegrationMessageConsumer<InventoryReserved>,
    IIntegrationMessageConsumer<InventoryReservationFailed>,
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderReserved>>,
    IIntegrationMessageConsumer<OrderPaymentInProcessRegistered>,
    IIntegrationMessageConsumer<PaymentCompleted>,
    IIntegrationMessageConsumer<OrderDelivered>,
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderPaid>>,
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderStockReleaseRequested>>,
    INotificationHandler<OutboxMessage<Domain.Orders.Events.OrderPaymentCancellationRequested>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExternalPublisher _publisher;
    private readonly ILogger<CreateOrderSagaHandler> _logger;

    public CreateOrderSagaHandler(IOrderRepository orderRepository, IExternalPublisher publisher, IUnitOfWork unitOfWork,
        ILogger<CreateOrderSagaHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
            new(notification.Content.OrderId, items) { Id = notification.Content.Id, UserId = notification.Content.UserId };

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

        if (order.Status is OrderStatus.Failed or OrderStatus.Canceled)
        {
            // The order was closed (for example by a timeout) before inventory answered: undo that reservation.
            _logger.LogWarning("InventoryReserved received for order {OrderId} in status {Status}; requesting stock release",
                order.Id, order.Status);
            order.RequestStockRelease("Stock reserved after the order was closed");
            await _unitOfWork.CommitAsync(cancellationToken);
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
        Nur.Store2026.Integration.Orders.OrderReserved integrationMessage = new(domainEvent.OrderId, domainEvent.TotalAmount)
            {
                Id = domainEvent.Id,
                CustomerName = domainEvent.CustomerName,
                CustomerDocument = domainEvent.CustomerDocument,
                Lines = domainEvent.Lines?
                    .Select(l => new OrderLine(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice))
                    .ToList()
            };

        return _publisher.PublishAsync(integrationMessage);
    }

    public async Task HandleAsync(OrderPaymentInProcessRegistered message, CancellationToken cancellationToken)
    {
        Order? order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            return;
        }
        if (order.Status is OrderStatus.Failed or OrderStatus.Canceled)
        {
            // The order was closed while payments was still registering the pending payment: cancel it.
            _logger.LogWarning("OrderPaymentInProcessRegistered received for order {OrderId} in status {Status}; requesting payment cancellation",
                order.Id, order.Status);
            order.RequestPaymentCancellation("Payment registered after the order was closed");
            await _unitOfWork.CommitAsync(cancellationToken);
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

    /// <summary>
    /// Payment confirmed: the order is paid. Late or duplicated messages are ignored.
    /// </summary>
    public async Task HandleAsync(PaymentCompleted message, CancellationToken cancellationToken)
    {
        Order? order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            _logger.LogWarning("PaymentCompleted received for unknown order {OrderId}", message.OrderId);
            return;
        }

        if (order.Status is OrderStatus.Paid or OrderStatus.Completed)
        {
            return; // duplicated message
        }

        if (order.Status is OrderStatus.Failed or OrderStatus.Canceled)
        {
            // Money was taken for an order that is already dead: needs a refund (not implemented yet).
            _logger.LogError("PaymentCompleted received for order {OrderId} in status {Status}; a refund is required",
                order.Id, order.Status);
            return;
        }

        // Throws if the order is not PaymentInProcess yet, so the broker redelivers it after
        // OrderPaymentInProcessRegistered has been processed.
        order.MarkAsPaid(message.InvoiceNumber);

        await _unitOfWork.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The customer picked the products up at the warehouse: the sale is completed.
    /// </summary>
    public async Task HandleAsync(OrderDelivered message, CancellationToken cancellationToken)
    {
        Order? order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            _logger.LogWarning("OrderDelivered received for unknown order {OrderId}", message.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Completed)
        {
            return; // duplicated message
        }

        if (order.Status != OrderStatus.Paid)
        {
            // Inventory only delivers paid orders, so this means the two services disagree: do not hide it.
            _logger.LogError("OrderDelivered received for order {OrderId} in status {Status}; it was not completed",
                order.Id, order.Status);
            return;
        }

        order.MarkAsCompleted();

        await _unitOfWork.CommitAsync(cancellationToken);
    }

    public Task Handle(OutboxMessage<Domain.Orders.Events.OrderPaid> notification, CancellationToken cancellationToken)
    {
        Nur.Store2026.Integration.Orders.OrderPaid integrationMessage = new(notification.Content.OrderId)
        {
            Id = notification.Content.Id,
            InvoiceNumber = notification.Content.InvoiceNumber,
            CustomerName = notification.Content.CustomerName,
            CustomerDocument = notification.Content.CustomerDocument
        };

        return _publisher.PublishAsync(integrationMessage);
    }

    public Task Handle(OutboxMessage<Domain.Orders.Events.OrderStockReleaseRequested> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.Content;
        Nur.Store2026.Integration.Orders.OrderStockReleaseRequested integrationMessage =
            new(domainEvent.OrderId, domainEvent.Reason) { Id = domainEvent.Id };

        return _publisher.PublishAsync(integrationMessage);
    }

    public Task Handle(OutboxMessage<Domain.Orders.Events.OrderPaymentCancellationRequested> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.Content;
        Nur.Store2026.Integration.Orders.OrderPaymentCancellationRequested integrationMessage =
            new(domainEvent.OrderId, domainEvent.Reason) { Id = domainEvent.Id };

        return _publisher.PublishAsync(integrationMessage);
    }
}
