using ChapGiti.Infrastructure.Persistence.Ef.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> entity)
    {
        entity.ToTable("Ticket_Attachments", t => t.HasCheckConstraint("CK_TicketAttachment_Size", "[size] > 0 AND [position] >= 0"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasMaxLength(36).IsUnicode(false);
        entity.Property(x => x.MessageId).HasColumnName("message_id").HasMaxLength(36).IsUnicode(false);
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160);
        entity.Property(x => x.Mime).HasColumnName("mime").HasMaxLength(32).IsUnicode(false);
        entity.Property(x => x.Size).HasColumnName("size");
        entity.Property(x => x.Position).HasColumnName("position");
        entity.Property(x => x.Digest).HasColumnName("digest").HasMaxLength(64).IsUnicode(false);
        entity.HasOne<TicketMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.MessageId, x.Position }).IsUnique();
    }
}
