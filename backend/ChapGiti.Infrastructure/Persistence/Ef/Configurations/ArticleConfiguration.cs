using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Ef.Configurations;

// Reverse-engineered from the validated immutable SQL001–009 contract.
internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> entity)
    {
        entity.HasKey(e => e.Id);

        entity.HasIndex(e => e.Slug, "UQ__Articles__32DD1E4CC118CAA6").IsUnique();

        entity.Property(e => e.Id)
            .HasMaxLength(36)
            .IsUnicode(false)
            .HasColumnName("id");
        entity.Property(e => e.Category)
            .HasMaxLength(60)
            .HasColumnName("category");
        entity.Property(e => e.Content).HasColumnName("content");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasColumnName("created_at");
        entity.Property(e => e.Excerpt)
            .HasMaxLength(400)
            .HasColumnName("excerpt");
        entity.Property(e => e.Published).HasColumnName("published");
        entity.Property(e => e.SeoDescription)
            .HasMaxLength(180)
            .HasDefaultValue("")
            .HasColumnName("seo_description");
        entity.Property(e => e.SeoTitle)
            .HasMaxLength(100)
            .HasDefaultValue("")
            .HasColumnName("seo_title");
        entity.Property(e => e.Slug)
            .HasMaxLength(120)
            .IsUnicode(false)
            .HasColumnName("slug");
        entity.Property(e => e.Title)
            .HasMaxLength(180)
            .HasColumnName("title");
        entity.Property(e => e.UpdatedAt)
            .HasPrecision(3)
            .HasColumnName("updated_at");
    }
}
