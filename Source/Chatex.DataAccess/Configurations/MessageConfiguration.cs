using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatex.DataAccess.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public const int MaxBodyLength = 4000;

    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(m => m.Body).HasMaxLength(MaxBodyLength);

        // Keyset pagination: WHERE chat_id = @c AND id < @cursor ORDER BY id DESC.
        builder.HasIndex(m => new { m.ChatId, m.Id }).IsDescending(false, true);
        builder.HasIndex(m => m.SenderId);

        builder.HasOne(m => m.Chat)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ReplyTo)
            .WithMany()
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
