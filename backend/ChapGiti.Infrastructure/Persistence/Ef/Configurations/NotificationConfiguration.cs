using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.HasIndex(e => new { e.AdminEmail, e.EntityId, e.Sequence }, "IX_Notifications_AdminOrderAttention")
            .IsDescending(false, false, true)
            .HasFilter("([admin_email] IS NOT NULL AND [entity_kind]='order')");

        entity.HasIndex(e => new { e.AdminEmail, e.Sequence }, "IX_Notifications_AdminTime")
            .IsDescending(false, true)
            .HasFilter("([admin_email] IS NOT NULL)");

        entity.HasIndex(e => new { e.AdminEmail, e.Sequence }, "IX_Notifications_AdminUnread")
            .IsDescending(false, true)
            .HasFilter("([admin_email] IS NOT NULL AND [read_at] IS NULL)");

        entity.HasIndex(e => new { e.CustomerId, e.EntityId, e.Sequence }, "IX_Notifications_CustomerOrderAttention")
            .IsDescending(false, false, true)
            .HasFilter("([customer_id] IS NOT NULL AND [entity_kind]='order')");

        entity.HasIndex(e => new { e.CustomerId, e.Sequence }, "IX_Notifications_CustomerTime")
            .IsDescending(false, true)
            .HasFilter("([customer_id] IS NOT NULL)");

        entity.HasIndex(e => new { e.CustomerId, e.Sequence }, "IX_Notifications_CustomerUnread")
            .IsDescending(false, true)
            .HasFilter("([customer_id] IS NOT NULL AND [read_at] IS NULL)");

        entity.HasIndex(e => new { e.AdminEmail, e.DedupeKey }, "UX_Notifications_AdminEvent")
            .IsUnique()
            .HasFilter("([admin_email] IS NOT NULL)");

        entity.HasIndex(e => new { e.CustomerId, e.DedupeKey }, "UX_Notifications_CustomerEvent")
            .IsUnique()
            .HasFilter("([customer_id] IS NOT NULL)");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.AdminEmail)
            .HasMaxLength(254)
            .HasColumnName("admin_email");
        entity.Property(e => e.Body)
            .HasMaxLength(500)
            .HasColumnName("body");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.DedupeKey)
            .HasMaxLength(100)
            .IsUnicode(false)
            .HasColumnName("dedupe_key");
        entity.Property(e => e.EntityId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("entity_id");
        entity.Property(e => e.EntityKind)
            .HasMaxLength(8)
            .IsUnicode(false)
            .HasColumnName("entity_kind");
        entity.Property(e => e.EventType)
            .HasMaxLength(48)
            .IsUnicode(false)
            .HasColumnName("event_type");
        entity.Property(e => e.ReadAt)
            .HasPrecision(3)
            .HasColumnName("read_at");
        entity.Property(e => e.RecipientKind)
            .HasMaxLength(8)
            .IsUnicode(false)
            .HasColumnName("recipient_kind");
        entity.Property(e => e.Sequence)
            .ValueGeneratedOnAdd()
            .HasColumnName("sequence");
        entity.Property(e => e.TargetPath)
            .HasMaxLength(200)
            .HasColumnName("target_path");
        entity.Property(e => e.Title)
            .HasMaxLength(120)
            .HasColumnName("title");

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.AdminEmail)
            .HasConstraintName("FK_Notifications_Admin");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.CustomerId)
            .HasConstraintName("FK_Notifications_Customer");
    }
}
