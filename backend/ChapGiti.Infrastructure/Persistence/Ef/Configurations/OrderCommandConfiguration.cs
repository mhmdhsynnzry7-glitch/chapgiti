using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class OrderCommandConfiguration : IEntityTypeConfiguration<OrderCommand>
{
    public void Configure(EntityTypeBuilder<OrderCommand> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Order_Commands");

        entity.HasIndex(e => new { e.OrderId, e.CreatedAt }, "IX_OrderCommands_Order");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.Actor)
            .HasMaxLength(280)
            .HasColumnName("actor");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Kind)
            .HasMaxLength(32)
            .IsUnicode(false)
            .HasColumnName("kind");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.PayloadHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("payload_hash");

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
