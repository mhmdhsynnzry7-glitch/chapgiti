using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class AuthRefreshCredentialConfiguration : IEntityTypeConfiguration<AuthRefreshCredential>
{
    public void Configure(EntityTypeBuilder<AuthRefreshCredential> entity)
    {
        entity.HasKey(e => e.TokenHash);

        entity.ToTable("Auth_Refresh_Credentials");

        entity.HasIndex(e => e.SessionId, "IX_Refresh_Session");

        entity.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("token_hash");
        entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        entity.Property(e => e.Expires).HasColumnName("expires");
        entity.Property(e => e.SessionId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("session_id");

        entity.HasOne<AuthSession>().WithMany()
            .HasForeignKey(d => d.SessionId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
