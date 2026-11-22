using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> entity)
    {
        entity.HasKey(e => e.TokenHash);

        entity.ToTable("Admin_Sessions");

        entity.HasIndex(e => e.Expires, "IX_AdminSession_Expires");

        entity.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("token_hash");
        entity.Property(e => e.Email)
            .HasMaxLength(254)
            .HasColumnName("email");
        entity.Property(e => e.Expires).HasColumnName("expires");

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.Email)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
