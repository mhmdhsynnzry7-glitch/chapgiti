using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class RateLimitConfiguration : IEntityTypeConfiguration<RateLimit>
{
    public void Configure(EntityTypeBuilder<RateLimit> entity)
    {
        entity.HasKey(e => e.Key);

        entity.ToTable("Rate_Limits");

        entity.Property(e => e.Key)
            .HasMaxLength(100)
            .IsUnicode(false)
            .HasColumnName("key");
        entity.Property(e => e.Count).HasColumnName("count");
        entity.Property(e => e.Expires).HasColumnName("expires");
    }
}
