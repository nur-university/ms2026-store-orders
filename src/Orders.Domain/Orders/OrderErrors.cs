using Joseco.DDD.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Domain.Orders;

public class OrderErrors
{
    public static Error PriceInvalid() => new("Price.Negative", "Price cannot be negative", ErrorType.Validation);
    public static Error QuantityInvalid() => new("Quantity.Negative", "Quantity cannot be negative", ErrorType.Validation);


    public static Error ReservedStatusNotAllowed(OrderStatus currentStatus) =>
        new("Order.ReserveNotAllowed", 
            $"Order is not allowed to mark as reserved. Order should be Started. The current status is { currentStatus }.", 
            ErrorType.Validation);

    public static Error PaymentInProcessStatusNotAllowed(OrderStatus status) =>
        new("Order.PaymentInProcessNotAllowed",
            $"Order is not allowed to mark as payment in process. Order should be Reserved. The current status is {status}.",
            ErrorType.Validation);

    public static Error PaidStatusNotAllowed(OrderStatus status) =>
        new("Order.PaidNotAllowed",
            $"Order is not allowed to mark as paid. Order should be PaymentInProcess. The current status is {status}.",
            ErrorType.Validation);

    public static Error CompletedStatusNotAllowed(OrderStatus status) =>
        new("Order.CompletedNotAllowed",
            $"Order is not allowed to mark as completed. Order should be Paid. The current status is {status}.",
            ErrorType.Validation);

    public static Error CanceledStatusNotAllowed(OrderStatus status) =>
        new("Order.CanceledNotAllowed",
            $"Only Started, Reserved or PaymentInProcess orders can be canceled. The current status is {status}.",
            ErrorType.Conflict);

    public static Error PaymentCancellationNotAllowed(OrderStatus status) =>
        new("Order.PaymentCancellationNotAllowed",
            $"A payment can only be canceled for a Failed or Canceled order. The current status is {status}.",
            ErrorType.Validation);

    public static Error NotADraft(OrderStatus status) =>
        new("Order.NotADraft", $"Only a draft can be edited, confirmed or discarded. The current status is {status}.", ErrorType.Conflict);

    public static Error AnnulNotAllowed(OrderStatus status) =>
        new("Order.AnnulNotAllowed",
            $"Only a paid order whose goods were not picked up yet can be annulled. The current status is {status}.",
            ErrorType.Conflict);

    public static Error AnnulReasonRequired() =>
        new("Order.AnnulReasonRequired", "The reason for annulling a paid sale is required", ErrorType.Validation);

    public static Error EmptyOrder() =>
        new("Order.Empty", "An order needs at least one item", ErrorType.Validation);

    public static Error ProductNotFound(Guid productId) =>
        new("Order.ProductNotFound", $"The product {productId} does not exist", ErrorType.NotFound);

    public static Error ProductPriceUnavailable(Guid productId) =>
        new("Order.ProductPriceUnavailable", $"The price of the product {productId} is not available yet", ErrorType.Conflict);

    public static Error OrderNotFound(Guid orderId) =>
        new("Order.NotFound", $"The order {orderId} does not exist", ErrorType.NotFound);

    public static Error StockReleaseNotAllowed(OrderStatus status) =>
        new("Order.StockReleaseNotAllowed",
            $"Stock can only be released for a Failed or Canceled order. The current status is {status}.",
            ErrorType.Validation);

    public static Error FailedStatusNotAllowed(OrderStatus status) =>
        new("Order.FailedNotAllowed",
            $"Order is not allowed to mark as failed. Order should be Completed, Canceled or Failed. The current status is {status}.",
            ErrorType.Validation);

}
