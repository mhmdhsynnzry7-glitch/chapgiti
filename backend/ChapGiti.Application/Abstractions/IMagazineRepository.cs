using ChapGiti.Application.Content;

namespace ChapGiti.Application.Abstractions;

public interface IMagazineRepository
{
    Task<bool> Available();
    Task<MagazinePage> List(bool admin,string query,string status,string category,int page,int pageSize, bool oldestFirst = false);
    Task<MagazineLookup?> PublicArticle(string slug);
    Task<MagazineArticle?> Find(string id);
    Task LockSlugs();
    Task<bool> SlugUsed(string slug,string exceptArticle);
    Task<bool> AssetExists(string id);
    Task<bool> Save(MagazineArticle article,int? expectedVersion,string? previousSlug);
    Task<IReadOnlyList<MagazineCard>> Related(string id,string category);
    Task<IReadOnlyList<MagazineSitemapItem>> Sitemap();
}
