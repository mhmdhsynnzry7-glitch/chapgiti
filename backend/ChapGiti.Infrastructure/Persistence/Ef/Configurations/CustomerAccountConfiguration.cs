using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
{
    public void Configure(EntityTypeBuilder<CustomerAccount> entity)
    {
        entity.HasKey(e => e.Phone);

        entity.ToTable("Customer_Accounts");

        entity.HasIndex(e => e.RegistrationId, "UQ__Customer__22A298F7098C67AA").IsUnique();

        entity.Property(e => e.Phone)
            .HasMaxLength(11)
            .IsUnicode(false)
            .HasColumnName("phone");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.PasswordHash)
            .HasMaxLength(512)
            .IsUnicode(false)
            .HasColumnName("password_hash");
        entity.Property(e => e.RegistrationId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("registration_id");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.RegistrationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
