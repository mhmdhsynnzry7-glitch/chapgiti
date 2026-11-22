using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class ImportHistoryConfiguration : IEntityTypeConfiguration<ImportHistory>
{
    public void Configure(EntityTypeBuilder<ImportHistory> entity)
    {
        entity.HasKey(e => e.SourceHash);

        entity.ToTable("Import_History");

        entity.Property(e => e.SourceHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .HasColumnName("source_hash");
        entity.Property(e => e.ImportedAt)
            .HasPrecision(3)
            .HasColumnName("imported_at");
    }
}
