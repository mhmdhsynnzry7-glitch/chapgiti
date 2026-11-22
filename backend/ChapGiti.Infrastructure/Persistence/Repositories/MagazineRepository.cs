using ChapGiti.Application.Content;
using ChapGiti.Infrastructure.Persistence.Ef;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using Microsoft.EntityFrameworkCore;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class MagazineRepository(SqlStore db) : IMagazineRepository
{
    public async Task<bool> Available() =>
        (await db.One("SELECT CASE WHEN COL_LENGTH('dbo.Articles','publication_status') IS NOT NULL AND OBJECT_ID('dbo.Article_Slug_Aliases','U') IS NOT NULL THEN 1 ELSE 0 END ready"))!
        .Num("ready") == 1;

    private IQueryable<Article> Articles => db.Context.Articles.AsNoTracking();

    private static MagazineArticle Map(Article a) => new(
        a.Id, a.Slug, a.Title, a.Excerpt, a.Content, a.Category, a.PublicationStatus,
        a.CoverAssetId, a.CoverAlt, a.PublicAuthorName, EfRows.Stamp(a.PublishedAt),
        EfRows.Stamp(a.CreatedAt)!, EfRows.Stamp(a.UpdatedAt)!, a.SeoTitle, a.SeoDescription,
        a.Version, a.ContentFormat);

    private static IQueryable<MagazineCard> Cards(IQueryable<Article> query) =>
        query.Select(a => new MagazineCard(
            a.Id, a.Slug, a.Title, a.Excerpt, a.Category, a.PublicationStatus,
            a.CoverAssetId == null ? null : "/api/banner-assets/" + a.CoverAssetId,
            a.CoverAlt, a.PublicAuthorName, EfRows.Stamp(a.PublishedAt), EfRows.Stamp(a.UpdatedAt)!, a.Version));

    public async Task<MagazinePage> List(bool admin, string query, string status, string category, int page, int pageSize, bool oldestFirst = false)
    {
        var visible = Articles.Where(a => admin || a.PublicationStatus == "published");
        var filter = visible.Where(a =>
            (query == "" || a.Title.Contains(query) || a.Excerpt.Contains(query)) &&
            (status == "" || a.PublicationStatus == status) &&
            (category == "" || a.Category == category));
        var orderedPage = (oldestFirst ? filter.OrderBy(a=>a.PublishedAt).ThenBy(a=>a.UpdatedAt).ThenBy(a=>a.Id) : filter
            .OrderByDescending(a => a.PublishedAt)
            .ThenByDescending(a => a.UpdatedAt)
            .ThenBy(a => a.Id))
            .Skip((page - 1) * pageSize).Take(pageSize);
        return new(
            await Cards(orderedPage).ToListAsync(), await filter.LongCountAsync(), page, pageSize,
            await visible.Select(a => a.Category).Where(c => c != "").Distinct().OrderBy(c => c).ToListAsync());
    }

    public async Task<MagazineLookup?> PublicArticle(string slug)
    {
        var article = await Articles.SingleOrDefaultAsync(a => a.Slug == slug && a.PublicationStatus == "published");
        if (article != null)
            return new(Map(article), false);
        article = await (
            from alias in db.Context.Set<ArticleSlugAlias>().AsNoTracking()
            join a in Articles on alias.ArticleId equals a.Id
            where alias.Slug == slug && a.PublicationStatus == "published"
            select a).SingleOrDefaultAsync();
        return article == null ? null : new(Map(article), true);
    }
    public async Task<MagazineArticle?> Find(string id)
    {
        var article = await Articles.SingleOrDefaultAsync(a => a.Id == id);
        return article == null ? null : Map(article);
    }
    public async Task LockSlugs() => await db.Execute(
        "DECLARE @r int; EXEC @r=sp_getapplock @Resource='ChapGiti.Magazine',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51111,'Magazine is busy.',1;");

    public async Task<bool> SlugUsed(string slug, string except) =>
        await Articles.AnyAsync(a => a.Slug == slug && a.Id != except) ||
        await db.Context.Set<ArticleSlugAlias>().AnyAsync(a => a.Slug == slug && a.ArticleId != except);

    public Task<bool> AssetExists(string id) => db.Context.BannerAssets.AnyAsync(a => a.Id == id);
    public async Task<bool> Save(MagazineArticle a, int? expected, string? previousSlug)
    {
        if (expected == null)
            await db.Insert(new Article
            {
                Id = a.Id,
                Slug = a.Slug,
                Title = a.Title,
                Excerpt = a.Excerpt,
                Content = a.Content,
                Category = a.Category,
                Published = a.Status == "published",
                PublicationStatus = a.Status,
                CoverAssetId = a.CoverAssetId,
                CoverAlt = a.CoverAlt,
                PublicAuthorName = a.AuthorName,
                PublishedAt = EfRows.Date(a.PublishedAt, true),
                CreatedAt = EfRows.Date(a.CreatedAt),
                UpdatedAt = EfRows.Date(a.UpdatedAt),
                SeoTitle = a.SeoTitle,
                SeoDescription = a.SeoDescription,
                Version = a.Version,
                ContentFormat = a.ContentFormat
            });
        else if (await db.Context.Articles
            .Where(x => x.Id == a.Id && x.Version == expected)
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Slug, a.Slug)
                .SetProperty(x => x.Title, a.Title)
                .SetProperty(x => x.Excerpt, a.Excerpt)
                .SetProperty(x => x.Content, a.Content)
                .SetProperty(x => x.Category, a.Category)
                .SetProperty(x => x.Published, a.Status == "published")
                .SetProperty(x => x.PublicationStatus, a.Status)
                .SetProperty(x => x.CoverAssetId, a.CoverAssetId)
                .SetProperty(x => x.CoverAlt, a.CoverAlt)
                .SetProperty(x => x.PublicAuthorName, a.AuthorName)
                .SetProperty(x => x.PublishedAt, EfRows.Date(a.PublishedAt, true))
                .SetProperty(x => x.UpdatedAt, EfRows.Date(a.UpdatedAt))
                .SetProperty(x => x.SeoTitle, a.SeoTitle)
                .SetProperty(x => x.SeoDescription, a.SeoDescription)
                .SetProperty(x => x.Version, a.Version)) != 1)
            return false;
        if (previousSlug != null && previousSlug != a.Slug && !await db.Context.Set<ArticleSlugAlias>().AnyAsync(x => x.Slug == previousSlug))
            await db.Insert(new ArticleSlugAlias { Slug = previousSlug, ArticleId = a.Id, CreatedAt = EfRows.Date(a.UpdatedAt) });
        // Returning to a former canonical slug must remove its own alias to avoid a self-redirect.
        await db.Context.Set<ArticleSlugAlias>().Where(x => x.Slug == a.Slug && x.ArticleId == a.Id).ExecuteDeleteAsync();
        return true;
    }
    public async Task<IReadOnlyList<MagazineCard>> Related(string id, string category) =>
        await Cards(Articles
            .Where(a => a.Id != id && a.Category == category && a.PublicationStatus == "published")
            .OrderByDescending(a => a.PublishedAt).ThenBy(a => a.Id).Take(3))
        .ToListAsync();

    public async Task<IReadOnlyList<MagazineSitemapItem>> Sitemap() =>
        await Articles.Where(a => a.PublicationStatus == "published")
            .OrderBy(a => a.Slug)
            .Select(a => new MagazineSitemapItem(a.Slug, EfRows.Stamp(a.UpdatedAt)!))
            .ToListAsync();
}
