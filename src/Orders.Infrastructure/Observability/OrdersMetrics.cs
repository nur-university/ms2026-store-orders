using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Orders.Domain.Orders;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orders.Infrastructure.Observability;

/// <summary>
/// Business metrics of the sales notes (meter <c>Store.Orders</c>). They are taken from the status changes the
/// unit of work is about to persist and only recorded once <c>SaveChanges</c> succeeded, so a retried or rolled
/// back commit is never counted.
/// </summary>
internal static class OrdersMetrics
{
    public const string MeterName = "Store.Orders";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Transitions = Meter.CreateCounter<long>("store.orders.transitions",
        description: "Sales note status changes; previous_status is 'none' for a newly created note.");

    private static readonly Histogram<double> LeadTime = Meter.CreateHistogram<double>("store.orders.lead_time",
        unit: "s", description: "Time from the start of a sales note until it reaches a status (Paid, Completed, Canceled, Failed).");

    internal readonly record struct Transition(string From, string To, double? LeadTimeSeconds, bool? Annulled);

    /// <summary>Reads the status changes staged in the change tracker. Never throws: metrics must not break a commit.</summary>
    public static IReadOnlyList<Transition> Capture(ChangeTracker tracker)
    {
        var transitions = new List<Transition>();
        try
        {
            foreach (EntityEntry<Order> entry in tracker.Entries<Order>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                {
                    continue;
                }

                OrderStatus? from = entry.State == EntityState.Added
                    ? null
                    : (OrderStatus)entry.Property(nameof(Order.Status)).OriginalValue!;
                Order order = entry.Entity;

                if (from == order.Status)
                {
                    continue;
                }

                DateTime? reachedOn = order.Status switch
                {
                    OrderStatus.Paid => order.PaidOn,
                    OrderStatus.Completed => order.CompletedOn,
                    OrderStatus.Canceled => order.CanceledOn,
                    OrderStatus.Failed => order.FailedOn,
                    _ => null
                };
                double? leadTime = reachedOn.HasValue
                    ? Math.Max(0, (reachedOn.Value - order.StartedOn).TotalSeconds)
                    : null;

                transitions.Add(new Transition(
                    from?.ToString() ?? "none",
                    order.Status.ToString(),
                    leadTime,
                    order.Status == OrderStatus.Canceled ? order.RefundRequired : null));
            }
        }
        catch
        {
            transitions.Clear();
        }

        return transitions;
    }

    public static void Record(IReadOnlyList<Transition> transitions)
    {
        foreach (Transition t in transitions)
        {
            var tags = new TagList { { "status", t.To }, { "previous_status", t.From } };
            if (t.Annulled.HasValue)
            {
                // A paid sale annulled by a supervisor: stock released, the money is handled outside the system.
                tags.Add("refund_required", t.Annulled.Value);
            }
            Transitions.Add(1, tags);

            if (t.LeadTimeSeconds.HasValue)
            {
                LeadTime.Record(t.LeadTimeSeconds.Value, new KeyValuePair<string, object?>("status", t.To));
            }
        }
    }
}
