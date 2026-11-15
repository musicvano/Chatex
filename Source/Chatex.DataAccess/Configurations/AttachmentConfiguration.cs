using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatex.DataAccess.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.Property(a => a.FileName).HasMaxLength(255);
        builder.Property(a => a.ContentType).HasMaxLength(127);
        builder.Property(a => a.StoragePath).HasMaxLength(260);

        builder.HasIndex(a => a.MessageId);
        builder.HasIndex(a => a.UploaderId);

        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Uploader)
            .WithMany()
            .HasForeignKey(a => a.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
