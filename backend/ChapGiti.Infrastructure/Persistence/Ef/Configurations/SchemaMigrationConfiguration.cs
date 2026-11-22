using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class SchemaMigrationConfiguration : IEntityTypeConfiguration<SchemaMigration>
{
    public void Configure(EntityTypeBuilder<SchemaMigration> entity)
    {
        entity.HasKey(e => e.Name);

        entity.ToTable("Schema_Migrations");

        entity.Property(e => e.Name)
            .HasMaxLength(150)
            .IsUnicode(false)
            .HasColumnName("name");
        entity.Property(e => e.AppliedAt)
            .HasPrecision(3)
            .HasColumnName("applied_at");
        entity.Property(e => e.Checksum)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("checksum");
    }
}
