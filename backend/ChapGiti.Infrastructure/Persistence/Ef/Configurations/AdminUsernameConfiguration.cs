using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class AdminUsernameConfiguration : IEntityTypeConfiguration<AdminUsername>
{
    public void Configure(EntityTypeBuilder<AdminUsername> entity)
    {
        entity.HasKey(e => e.Username);

        entity.ToTable("Admin_Usernames");

        entity.HasIndex(e => e.Email, "UQ__Admin_Us__AB6E61645CE3DD66").IsUnique();

        entity.Property(e => e.Username)
            .HasMaxLength(32)
            .IsUnicode(false)
            .HasColumnName("username");
        entity.Property(e => e.Email)
            .HasMaxLength(254)
            .HasColumnName("email");

        entity.HasOne<AdminUser>().WithMany()
            .HasForeignKey(d => d.Email)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
