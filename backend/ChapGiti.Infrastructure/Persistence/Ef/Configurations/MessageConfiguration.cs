using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Message1).HasColumnName("message");
        entity.Property(e => e.Name)
            .HasMaxLength(180)
            .HasColumnName("name");
        entity.Property(e => e.Phone)
            .HasMaxLength(11)
            .IsUnicode(false)
            .HasColumnName("phone");
        entity.Property(e => e.Read).HasColumnName("read");
        entity.Property(e => e.Subject)
            .HasMaxLength(160)
            .HasColumnName("subject");
    }
}
