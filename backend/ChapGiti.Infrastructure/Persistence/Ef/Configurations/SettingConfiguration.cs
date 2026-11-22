using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> entity)
    {
        entity.HasKey(e => e.Key);

        entity.Property(e => e.Key)
            .HasMaxLength(80)
            .HasColumnName("key");
        entity.Property(e => e.Value).HasColumnName("value");
    }
}
