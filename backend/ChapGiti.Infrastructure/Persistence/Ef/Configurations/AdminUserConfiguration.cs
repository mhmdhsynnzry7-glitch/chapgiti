using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> entity)
    {
        entity.HasKey(e => e.Email);

        entity.ToTable("Admin_Users");

        entity.HasIndex(e => e.EffectiveEmail, "UX_AdminUsers_EffectiveEmail").IsUnique();

        entity.Property(e => e.Email)
            .HasMaxLength(254)
            .HasColumnName("email");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.EffectiveEmail)
            .HasMaxLength(254)
            .HasComputedColumnSql("(isnull([login_email],[email]))", true)
            .HasColumnName("effective_email");
        entity.Property(e => e.LoginEmail)
            .HasMaxLength(254)
            .HasColumnName("login_email");
        entity.Property(e => e.PasswordHash)
            .HasMaxLength(512)
            .IsUnicode(false)
            .HasColumnName("password_hash");
    }
}
