using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using File = ChapGiti.Infrastructure.Persistence.Ef.Models.File;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.CreatedAt, "IX_Orders_Created").IsDescending();

        entity.HasIndex(e => e.CustomerId, "IX_Orders_Customer");

        entity.HasIndex(e => e.Phone, "IX_Orders_Phone");

        entity.HasIndex(e => e.Status, "IX_Orders_Status");

        entity.HasIndex(e => e.Code, "UQ__Orders__357D4CF980B564C5").IsUnique();

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.AdminNotes)
            .HasDefaultValue("")
            .HasColumnName("admin_notes");
        entity.Property(e => e.ApprovedFileId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("approved_file_id");
        entity.Property(e => e.Code)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasColumnName("code");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Customer)
            .HasMaxLength(180)
            .HasColumnName("customer");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.Email)
            .HasMaxLength(254)
            .HasDefaultValue("")
            .HasColumnName("email");
        entity.Property(e => e.Estimate).HasColumnName("estimate");
        entity.Property(e => e.FinalPrice).HasColumnName("final_price");
        entity.Property(e => e.InformationRequest)
            .HasMaxLength(900)
            .HasColumnName("information_request");
        entity.Property(e => e.InformationRequiresFile).HasColumnName("information_requires_file");
        entity.Property(e => e.Notes)
            .HasMaxLength(3000)
            .HasDefaultValue("")
            .HasColumnName("notes");
        entity.Property(e => e.Phone)
            .HasMaxLength(11)
            .IsUnicode(false)
            .HasColumnName("phone");
        entity.Property(e => e.Service)
            .HasMaxLength(60)
            .HasColumnName("service");
        entity.Property(e => e.Spec).HasColumnName("spec");
        entity.Property(e => e.Status)
            .HasMaxLength(40)
            .IsUnicode(false)
            .HasDefaultValue("new")
            .HasColumnName("status");
        entity.Property(e => e.Title)
            .HasMaxLength(160)
            .HasColumnName("title");
        entity.Property(e => e.UpdatedAt)
            .HasPrecision(3)
            .HasColumnName("updated_at");
        entity.Property(e => e.Version)
            .HasDefaultValue(1)
            .HasColumnName("version");
        entity.Property(e => e.WorkflowVersion)
            .HasDefaultValue((byte)2, "DF_Orders_Workflow")
            .HasColumnName("workflow_version");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.CustomerId)
            ;

        entity.HasOne<File>().WithMany()
            .HasPrincipalKey(p => new { p.OrderId, p.Id })
            .HasForeignKey(d => new { d.Id, d.ApprovedFileId })
            .HasConstraintName("FK_Orders_ApprovedFile");
    }
}
