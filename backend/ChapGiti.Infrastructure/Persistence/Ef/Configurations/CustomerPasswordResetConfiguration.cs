using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class CustomerPasswordResetConfiguration : IEntityTypeConfiguration<CustomerPasswordReset>
{
    public void Configure(EntityTypeBuilder<CustomerPasswordReset> entity)
    {
        entity.HasKey(e => e.TokenHash);

        entity.ToTable("Customer_Password_Resets");

        entity.HasIndex(e => new { e.CustomerId, e.Expires }, "IX_PasswordReset_Customer");

        entity.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("token_hash");
        entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.Expires).HasColumnName("expires");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
