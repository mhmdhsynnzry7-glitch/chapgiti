using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Ticket_Messages");

        entity.HasIndex(e => new { e.TicketId, e.CreatedAt }, "IX_TicketMessages_Ticket");

        entity.HasIndex(e => new { e.Actor, e.TicketId }, "IX_TicketMessages_Unread").HasFilter("[read_at] IS NULL");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.Actor)
            .HasMaxLength(12)
            .IsUnicode(false)
            .HasColumnName("actor");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.ReadAt).HasPrecision(3).HasColumnName("read_at");
        entity.Property(e => e.Message).HasColumnName("message");
        entity.Property(e => e.TicketId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("ticket_id");

        entity.HasOne<Ticket>().WithMany()
            .HasForeignKey(d => d.TicketId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
