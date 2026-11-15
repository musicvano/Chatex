using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatex.DataAccess.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.DisplayName).HasMaxLength(64);
        builder.Property(p => p.Bio).HasMaxLength(500);
        builder.Property(p => p.AvatarPath).HasMaxLength(260);
    }
}
