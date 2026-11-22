using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

internal sealed class PrintCatalogOptionConfiguration : IEntityTypeConfiguration<PrintCatalogOption>
{
    public void Configure(EntityTypeBuilder<PrintCatalogOption> e)
    {
        e.ToTable("Print_Catalog_Options", t =>
        {
            t.HasCheckConstraint("CK_PrintCatalog_Metadata", "ISJSON([metadata])=1");
            t.HasCheckConstraint("CK_PrintCatalog_Default", "[is_default]=0 OR [active]=1");
            t.HasCheckConstraint("CK_PrintCatalog_Version", "[version]>=1 AND [display_order]>=0");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36).IsUnicode(false);
        e.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(40).IsUnicode(false);
        e.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsUnicode(false);
        e.Property(x => x.Label).HasColumnName("label").HasMaxLength(160);
        e.Property(x => x.PricingRule).HasColumnName("pricing_rule").HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("nvarchar(max)");
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.Active).HasColumnName("active");
        e.Property(x => x.IsDefault).HasColumnName("is_default");
        e.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3);
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(3);
        e.HasIndex(x => new { x.Kind, x.Code }).IsUnique().HasDatabaseName("UX_PrintCatalog_Code");
        e.HasIndex(x => x.Kind).IsUnique().HasFilter("[is_default]=1 AND [active]=1").HasDatabaseName("UX_PrintCatalog_Default");
        e.HasIndex(x => new { x.Kind, x.Active, x.DisplayOrder, x.Code }).HasDatabaseName("IX_PrintCatalog_List");
    }
}
