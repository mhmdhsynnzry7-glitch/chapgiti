using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class CustomerRoleConfiguration : IEntityTypeConfiguration<CustomerRole>
{
    public void Configure(EntityTypeBuilder<CustomerRole> entity)
    {
        entity.HasKey(e => new { e.CustomerId, e.Role });

        entity.ToTable("Customer_Roles");

        entity.Property(e => e.CustomerId)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("customer_id");
        entity.Property(e => e.Role)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasColumnName("role");
        entity.Property(e => e.AssignedAt)
            .HasPrecision(3)
            .HasColumnName("assigned_at");
        entity.Property(e => e.AssignedBy)
            .HasMaxLength(254)
            .HasColumnName("assigned_by");

        entity.HasOne<CustomerAccount>().WithMany()
            .HasPrincipalKey(p => p.RegistrationId)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            ;
    }
}
