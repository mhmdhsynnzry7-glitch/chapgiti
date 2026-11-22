using ChapGiti.Infrastructure.Persistence.Ef.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

internal sealed class CutTariffVersionConfiguration : IEntityTypeConfiguration<CutTariffVersion>
{
    public void Configure(EntityTypeBuilder<CutTariffVersion> e)
    {
        e.ToTable("Cut_Tariff_Versions", table =>
        {
            table.HasCheckConstraint("CK_CutTariff_Version", "[version] >= 1");
            table.HasCheckConstraint("CK_CutTariff_Rates", "ISJSON([rates]) = 1");
        });
        e.HasKey(r => new { r.CutId, r.Version });
        e.Property(r => r.CutId).HasColumnName("cut_id").HasMaxLength(36).IsUnicode(false);
        e.Property(r => r.Version).HasColumnName("version").ValueGeneratedNever();
        e.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasPrecision(3);
        e.Property(r => r.Rates).HasColumnName("rates").HasColumnType("nvarchar(max)");
        e.HasOne<PrintCatalogOption>().WithMany().HasForeignKey(r => r.CutId).OnDelete(DeleteBehavior.Restrict);
    }
}
