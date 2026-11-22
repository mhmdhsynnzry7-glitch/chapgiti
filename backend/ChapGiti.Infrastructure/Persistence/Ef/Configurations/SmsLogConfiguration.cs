using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class SmsLogConfiguration : IEntityTypeConfiguration<SmsLog>
{
    public void Configure(EntityTypeBuilder<SmsLog> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Sms_Logs");

        entity.HasIndex(e => e.EventId, "UQ__Sms_Logs__2370F726893D3D59").IsUnique();

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Error)
            .HasMaxLength(100)
            .IsUnicode(false)
            .HasColumnName("error");
        entity.Property(e => e.EventId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("event_id");
        entity.Property(e => e.OrderId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("order_id");
        entity.Property(e => e.Phone)
            .HasMaxLength(11)
            .IsUnicode(false)
            .HasColumnName("phone");
        entity.Property(e => e.Receipt)
            .HasMaxLength(80)
            .IsUnicode(false)
            .HasColumnName("receipt");
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
