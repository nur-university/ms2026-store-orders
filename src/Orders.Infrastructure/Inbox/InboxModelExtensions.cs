using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure.Inbox;

internal static class InboxModelExtensions
{
    public static ModelBuilder AddInboxModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inboxMessage");
            builder.HasKey(x => new { x.MessageId, x.Consumer });
            builder.Property(x => x.MessageId).HasColumnName("messageId");
            builder.Property(x => x.Consumer).HasColumnName("consumer").HasMaxLength(200);
            builder.Property(x => x.ProcessedOn).HasColumnName("processedOn");
        });

        return modelBuilder;
    }
}
