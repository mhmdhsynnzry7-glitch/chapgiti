using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> entity)
    {
        entity.HasKey(e => e.Id);

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
        entity.Property(e => e.Note)
            .HasMaxLength(1000)
            .HasColumnName("note");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.Status)
            .HasMaxLength(40)
            .IsUnicode(false)
            .HasColumnName("status");

        entity.HasOne<Order>().WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
