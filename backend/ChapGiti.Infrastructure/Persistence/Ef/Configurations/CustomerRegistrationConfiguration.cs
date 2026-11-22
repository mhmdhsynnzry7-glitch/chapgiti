using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class CustomerRegistrationConfiguration : IEntityTypeConfiguration<CustomerRegistration>
{
    public void Configure(EntityTypeBuilder<CustomerRegistration> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Customer_Registrations");

        entity.HasIndex(e => e.Phone, "IX_Customers_Phone");

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.FirstName)
            .HasMaxLength(80)
            .HasDefaultValue("")
            .HasColumnName("first_name");
        entity.Property(e => e.LastName)
            .HasMaxLength(80)
            .HasDefaultValue("")
            .HasColumnName("last_name");
        entity.Property(e => e.Phone)
            .HasMaxLength(11)
            .IsUnicode(false)
            .HasColumnName("phone");
        entity.Property(e => e.PhoneVerified).HasColumnName("phone_verified");
        entity.Property(e => e.UpdatedAt)
            .HasPrecision(3)
            .HasColumnName("updated_at");
    }
}
