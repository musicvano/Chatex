using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatex.DataAccess.Configurations;

internal sealed class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_chats_direct_key",
            "(type = 'Direct') = (direct_key IS NOT NULL)"));

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.Title).HasMaxLength(128);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.AvatarPath).HasMaxLength(260);
        builder.Property(c => c.PublicHandle).HasMaxLength(32);
        builder.Property(c => c.DirectKey).HasMaxLength(65);

        builder.HasIndex(c => c.PublicHandle).IsUnique();
        builder.HasIndex(c => c.DirectKey).IsUnique();

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
