using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using File = ChapGiti.Infrastructure.Persistence.Ef.Models.File;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class FileConfiguration : IEntityTypeConfiguration<File>
{
    public void Configure(EntityTypeBuilder<File> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.ObjectKey, "UQ__Files__F08A5D4379DA58D1").IsUnique();

        entity.HasIndex(e => new { e.OrderId, e.Id }, "UX_Files_OrderReference").IsUnique();

        entity.HasIndex(e => new { e.OrderId, e.FileVersion }, "UX_Files_OrderVersion")
            .IsUnique()
            .HasFilter("([file_version] IS NOT NULL)");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.CorrectionId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("correction_id");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.FileVersion).HasColumnName("file_version");
        entity.Property(e => e.Name)
            .HasMaxLength(160)
            .HasColumnName("name");
        entity.Property(e => e.ObjectKey)
            .HasMaxLength(100)
            .IsUnicode(false)
            .HasColumnName("object_key");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.Size).HasColumnName("size");

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;

        entity.HasOne<OrderCorrection>().WithMany()
            .HasPrincipalKey(p => new { p.OrderId, p.Id })
            .HasForeignKey(d => new { d.OrderId, d.CorrectionId })
            .HasConstraintName("FK_Files_Correction");
    }
}
