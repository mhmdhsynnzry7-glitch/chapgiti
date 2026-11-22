using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class BannerAssetConfiguration : IEntityTypeConfiguration<BannerAsset>
{
    public void Configure(EntityTypeBuilder<BannerAsset> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Banner_Assets");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Mime)
            .HasMaxLength(40)
            .IsUnicode(false)
            .HasColumnName("mime");
        entity.Property(e => e.Size).HasColumnName("size");
    }
}
