using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

internal sealed class ArticleMetadataConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> e)
    {
        e.ToTable("Articles",t=>
        {
            t.HasCheckConstraint("CK_Article_Status","[publication_status] IN ('draft','published','archived') AND ([published]=1 AND [publication_status]='published' OR [published]=0 AND [publication_status]<>'published')");
            t.HasCheckConstraint("CK_Article_Version","[version]>=1");
            t.HasCheckConstraint("CK_Article_Format","[content_format]='markdown-v1'");
        });
        e.Property(x=>x.PublicationStatus).HasColumnName("publication_status").HasMaxLength(16).IsUnicode(false).HasDefaultValue("draft");
        e.Property(x=>x.CoverAssetId).HasColumnName("cover_asset_id").HasMaxLength(36).IsUnicode(false);
        e.Property(x=>x.CoverAlt).HasColumnName("cover_alt").HasMaxLength(250).HasDefaultValue("");
        e.Property(x=>x.PublicAuthorName).HasColumnName("public_author_name").HasMaxLength(120);
        e.Property(x=>x.PublishedAt).HasColumnName("published_at").HasPrecision(3);
        e.Property(x=>x.Version).HasColumnName("version").HasDefaultValue(1).IsConcurrencyToken();
        e.Property(x=>x.ContentFormat).HasColumnName("content_format").HasMaxLength(24).IsUnicode(false).HasDefaultValue("markdown-v1");
        e.HasOne<BannerAsset>().WithMany().HasForeignKey(x=>x.CoverAssetId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Articles_CoverAsset");
        e.HasIndex(x=>new{x.PublicationStatus,x.PublishedAt,x.Id}).HasDatabaseName("IX_Articles_PublicList");
        e.HasIndex(x=>new{x.Category,x.PublicationStatus}).HasDatabaseName("IX_Articles_CategoryStatus");
    }
}
internal sealed class ArticleSlugAliasConfiguration : IEntityTypeConfiguration<ArticleSlugAlias>
{
    public void Configure(EntityTypeBuilder<ArticleSlugAlias> e)
    {
        e.ToTable("Article_Slug_Aliases");e.HasKey(x=>x.Slug);
        e.Property(x=>x.Slug).HasColumnName("slug").HasMaxLength(120).IsUnicode(false);
        e.Property(x=>x.ArticleId).HasColumnName("article_id").HasMaxLength(36).IsUnicode(false);
        e.Property(x=>x.CreatedAt).HasColumnName("created_at").HasPrecision(3);
        e.HasOne<Article>().WithMany().HasForeignKey(x=>x.ArticleId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ArticleAliases_Article");
        e.HasIndex(x=>x.ArticleId).HasDatabaseName("IX_ArticleAliases_Article");
    }
}
