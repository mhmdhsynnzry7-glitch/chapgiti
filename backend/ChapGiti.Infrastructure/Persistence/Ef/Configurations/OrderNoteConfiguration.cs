using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class OrderNoteConfiguration : IEntityTypeConfiguration<OrderNote>
{
    public void Configure(EntityTypeBuilder<OrderNote> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Order_Notes");

        entity.HasIndex(e => new { e.OrderId, e.CreatedAt }, "IX_OrderNotes_Order").IsDescending(false, true);

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.Actor)
            .HasMaxLength(254)
            .HasColumnName("actor");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Note).HasColumnName("note");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
