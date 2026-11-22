using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class OrderViewConfiguration : IEntityTypeConfiguration<OrderView>
{
    public void Configure(EntityTypeBuilder<OrderView> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_OrderViews");

        entity.ToTable("Order_Views");

        entity.HasIndex(e => new { e.OrderId, e.AdminEmail }, "UX_OrderViews_Admin")
            .IsUnique()
            .HasFilter("([admin_email] IS NOT NULL)");

        entity.HasIndex(e => new { e.OrderId, e.CustomerId }, "UX_OrderViews_Customer")
            .IsUnique()
            .HasFilter("([customer_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AdminEmail)
            .HasMaxLength(254)
            .HasColumnName("admin_email");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.RecipientKind)
            .HasMaxLength(8)
            .IsUnicode(false)
            .HasColumnName("recipient_kind");
        entity.Property(e => e.SeenAt)
            .HasPrecision(3)
            .HasColumnName("seen_at");
        entity.Property(e => e.SeenThroughSequence).HasColumnName("seen_through_sequence");

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.AdminEmail)
            ;

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.CustomerId)
            ;

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
