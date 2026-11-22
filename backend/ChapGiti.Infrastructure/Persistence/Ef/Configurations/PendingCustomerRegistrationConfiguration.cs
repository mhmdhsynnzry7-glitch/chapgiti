using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class PendingCustomerRegistrationConfiguration : IEntityTypeConfiguration<PendingCustomerRegistration>
{
    public void Configure(EntityTypeBuilder<PendingCustomerRegistration> entity)
    {
        entity.HasKey(e => e.ChallengeId);

        entity.ToTable("Pending_Customer_Registrations");

        entity.Property(e => e.ChallengeId)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("challenge_id");
        entity.Property(e => e.FirstName)
            .HasMaxLength(80)
            .HasColumnName("first_name");
        entity.Property(e => e.LastName)
            .HasMaxLength(80)
            .HasColumnName("last_name");
        entity.Property(e => e.PasswordHash)
            .HasMaxLength(512)
            .IsUnicode(false)
            .HasColumnName("password_hash");

        entity.HasOne<CustomerOtpChallenge>().WithMany()
            .HasForeignKey(d => d.ChallengeId)
            ;
    }
}
