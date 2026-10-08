namespace Orders.Infrastructure.Inbox;

/// <summary>
/// Records that a consumer already processed an integration message, so a redelivered or republished
/// message (the broker and the outbox are at-least-once) is not applied twice.
/// </summary>
internal class InboxMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = null!;
    public DateTime ProcessedOn { get; set; }
}
