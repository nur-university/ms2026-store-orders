namespace Orders.Application.Orders.UseCases.ExpireOrder;

/// <summary>How long an order may stay in each intermediate status before the saga gives up on it ("OrderTimeouts" section).</summary>
public class OrderTimeoutOptions
{
    public const string SectionName = "OrderTimeouts";

    /// <summary>Started: waiting for inventory to answer.</summary>
    public TimeSpan StartedTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Reserved: waiting for payments to register the pending payment.</summary>
    public TimeSpan ReservedTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>PaymentInProcess: waiting for the payment of the customer.</summary>
    public TimeSpan PaymentTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How often the worker looks for stale orders.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int BatchSize { get; set; } = 50;
}
