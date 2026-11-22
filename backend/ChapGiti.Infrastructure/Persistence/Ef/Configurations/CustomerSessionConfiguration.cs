using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> entity)
    {
        entity.HasKey(e => e.TokenHash);

        entity.ToTable("Customer_Sessions");

        entity.HasIndex(e => e.Expires, "IX_CustomerSession_Expires");

        entity.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("token_hash");
        entity.Property(e => e.Expires).HasColumnName("expires");
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
