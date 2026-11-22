namespace ChapGiti.Application.Content;

public sealed record MagazineArticle(string Id,string Slug,string Title,string Excerpt,string Content,string Category,string Status,string? CoverAssetId,string CoverAlt,string? AuthorName,string? PublishedAt,string CreatedAt,string UpdatedAt,string SeoTitle,string SeoDescription,int Version,string ContentFormat="markdown-v1");
public sealed record MagazineCard(string Id,string Slug,string Title,string Excerpt,string Category,string Status,string? CoverUrl,string CoverAlt,string? AuthorName,string? PublishedAt,string UpdatedAt,int Version);
public sealed record MagazinePage(IReadOnlyList<MagazineCard> Items,long Total,int Page,int PageSize,IReadOnlyList<string> Categories);
public sealed record MagazineLookup(MagazineArticle Article,bool Alias);
public sealed record MagazineSitemapItem(string Slug,string UpdatedAt);
