using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using File = ChapGiti.Infrastructure.Persistence.Ef.Models.File;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class OrderCorrectionConfiguration : IEntityTypeConfiguration<OrderCorrection>
{
    public void Configure(EntityTypeBuilder<OrderCorrection> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_OrderCorrections");

        entity.ToTable("Order_Corrections");

        entity.HasIndex(e => new { e.OrderId, e.Revision }, "IX_OrderCorrections_History").IsDescending(false, true);

        entity.HasIndex(e => new { e.OrderId, e.Id }, "UQ_OrderCorrections_OrderReference").IsUnique();

        entity.HasIndex(e => new { e.OrderId, e.Revision }, "UQ_OrderCorrections_Revision").IsUnique();

        entity.HasIndex(e => e.OrderId, "UX_OrderCorrections_Active")
            .IsUnique()
            .HasFilter("([is_active]=(1))");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.ApprovedFileId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("approved_file_id");
        entity.Property(e => e.BaseFileId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("base_file_id");
        entity.Property(e => e.FileId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("file_id");
        entity.Property(e => e.IsActive).HasColumnName("is_active");
        entity.Property(e => e.IsLegacy).HasColumnName("is_legacy");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.RequestText)
            .HasMaxLength(900)
            .HasColumnName("request_text");
        entity.Property(e => e.RequestedAt)
            .HasPrecision(3)
            .HasColumnName("requested_at");
        entity.Property(e => e.RequestedByAdmin)
            .HasMaxLength(254)
            .HasColumnName("requested_by_admin");
        entity.Property(e => e.RequiresNewPdf).HasColumnName("requires_new_pdf");
        entity.Property(e => e.ReviewReason)
            .HasMaxLength(900)
            .HasColumnName("review_reason");
        entity.Property(e => e.ReviewedAt)
            .HasPrecision(3)
            .HasColumnName("reviewed_at");
        entity.Property(e => e.ReviewedByAdmin)
            .HasMaxLength(254)
            .HasColumnName("reviewed_by_admin");
        entity.Property(e => e.Revision).HasColumnName("revision");
        entity.Property(e => e.State)
            .HasMaxLength(16)
            .IsUnicode(false)
            .HasColumnName("state");
        entity.Property(e => e.SubmissionText)
            .HasMaxLength(3000)
            .HasColumnName("submission_text");
        entity.Property(e => e.SubmittedAt)
            .HasPrecision(3)
            .HasColumnName("submitted_at");
        entity.Property(e => e.SubmittedByCustomer)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("submitted_by_customer");
        entity.Property(e => e.Version)
            .HasDefaultValue(1, "DF_OrderCorrections_Version")
            .HasColumnName("version");

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.RequestedByAdmin)
            ;

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.ReviewedByAdmin)
            ;

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.SubmittedByCustomer)
            ;

        entity.HasOne<File>().WithMany()
            .HasPrincipalKey(p => new { p.OrderId, p.Id })
            .HasForeignKey(d => new { d.OrderId, d.ApprovedFileId })
            .HasConstraintName("FK_OrderCorrections_ApprovedFile");

        entity.HasOne<File>().WithMany()
            .HasPrincipalKey(p => new { p.OrderId, p.Id })
            .HasForeignKey(d => new { d.OrderId, d.BaseFileId })
            .HasConstraintName("FK_OrderCorrections_BaseFile");

        entity.HasOne<File>().WithMany()
            .HasPrincipalKey(p => new { p.OrderId, p.Id })
            .HasForeignKey(d => new { d.OrderId, d.FileId })
            .HasConstraintName("FK_OrderCorrections_SubmittedFile");
    }
}
