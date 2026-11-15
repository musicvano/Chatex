using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatex.DataAccess.Configurations;

internal sealed class ChatMemberConfiguration : IEntityTypeConfiguration<ChatMember>
{
    public void Configure(EntityTypeBuilder<ChatMember> builder)
    {
        builder.HasKey(m => new { m.ChatId, m.UserId });

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);

        // Lists the chats of a user; the primary key already covers lookups by chat.
        builder.HasIndex(m => m.UserId);

        builder.HasOne(m => m.Chat)
            .WithMany(c => c.Members)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
