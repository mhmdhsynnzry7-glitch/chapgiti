using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class CustomerOtpChallengeConfiguration : IEntityTypeConfiguration<CustomerOtpChallenge>
{
    public void Configure(EntityTypeBuilder<CustomerOtpChallenge> entity)
    {
        entity.HasKey(e => e.Id);

        entity.ToTable("Customer_Otp_Challenges");

        entity.HasIndex(e => new { e.Phone, e.CreatedAt }, "IX_Otp_Phone_Created").IsDescending(false, true);

        entity.Property(e => e.Id)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("id");
        entity.Property(e => e.Attempts).HasColumnName("attempts");
        entity.Property(e => e.CodeHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("code_hash");
        entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.DeliveryStatus)
            .HasMaxLength(12)
            .IsUnicode(false)
            .HasDefaultValue("pending")
            .HasColumnName("delivery_status");
        entity.Property(e => e.Expires).HasColumnName("expires");
        entity.Property(e => e.Phone)
            .HasMaxLength(11)
            .IsUnicode(false)
            .HasColumnName("phone");
        entity.Property(e => e.Purpose)
            .HasMaxLength(16)
            .IsUnicode(false)
            .HasColumnName("purpose");

        entity.HasOne<CustomerRegistration>().WithMany()
            .HasForeignKey(d => d.CustomerId)
            ;
    }
}
