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
            $"Order is not allowed to mark as canceled. Order should be Completed or Canceled. The current status is {status}.",
            ErrorType.Validation);

    public static Error StockReleaseNotAllowed(OrderStatus status) =>
        new("Order.StockReleaseNotAllowed",
            $"Stock can only be released for a Failed or Canceled order. The current status is {status}.",
            ErrorType.Validation);

    public static Error FailedStatusNotAllowed(OrderStatus status) =>
        new("Order.FailedNotAllowed",
            $"Order is not allowed to mark as failed. Order should be Completed, Canceled or Failed. The current status is {status}.",
            ErrorType.Validation);

}
