using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => new { e.CustomerId, e.UpdatedAt }, "IX_Tickets_Customer").IsDescending(false, true);

        entity.HasIndex(e => new { e.OrderId, e.UpdatedAt }, "IX_Tickets_Order").IsDescending(false, true);

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.Status)
            .HasMaxLength(24)
            .IsUnicode(false)
            .HasDefaultValue("open")
            .HasColumnName("status");
        entity.Property(e => e.Subject)
            .HasMaxLength(160)
            .HasColumnName("subject");
        entity.Property(e => e.UpdatedAt)
            .HasPrecision(3)
            .HasColumnName("updated_at");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .HasConstraintName("FK_Tickets_Order");
    }
}
