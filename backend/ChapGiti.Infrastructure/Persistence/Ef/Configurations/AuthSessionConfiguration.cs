using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Auth_Sessions");

        entity.HasIndex(e => new { e.LegacyAdminEmail, e.RevokedAt }, "IX_AuthSession_Admin");

        entity.HasIndex(e => new { e.CustomerId, e.RevokedAt }, "IX_AuthSession_Customer");

        entity.HasIndex(e => e.AccessHash, "UQ__Auth_Ses__A7E9604A5DAAAABF").IsUnique();

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.AccessExpires).HasColumnName("access_expires");
        entity.Property(e => e.AccessHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("access_hash");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.Expires).HasColumnName("expires");
        entity.Property(e => e.LegacyAdminEmail)
            .HasMaxLength(254)
            .HasColumnName("legacy_admin_email");
        entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");

        entity.HasOne<CustomerAccount>().WithMany()
            .HasPrincipalKey(p => p.RegistrationId)
            .HasForeignKey(d => d.CustomerId)
            ;

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.LegacyAdminEmail)
            ;
    }
}
