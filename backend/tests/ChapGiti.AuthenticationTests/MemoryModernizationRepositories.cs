using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Catalog;
using ChapGiti.Application.Content;
using ChapGiti.Infrastructure;

namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryPrintCatalogRepository(MemoryApiData data) : IPrintCatalogRepository
{
    public static Dictionary<string,PrintCatalogItem> Initial()
    {
        using var stream=typeof(DependencyInjection).Assembly.GetManifestResourceStream("ChapGiti.InitialPrintCatalog.json")!;
        using var reader=new StreamReader(stream);
        var array=JsonNode.Parse(reader.ReadToEnd())!.AsArray();
        return array.Select(node=>{var n=node!.AsObject();return new PrintCatalogItem(n["id"]!.ToString(),n["kind"]!.ToString(),n["code"]!.ToString(),n["label"]!.ToString(),n["pricingRule"]!.ToString(),n["metadata"]!.DeepClone().AsObject(),n["displayOrder"]!.GetValue<int>(),true,n["isDefault"]!.GetValue<bool>(),1,"2026-01-01T00:00:00.000Z","2026-01-01T00:00:00.000Z");}).ToDictionary(i=>i.Id);
    }
    public Task<bool> Available()=>Task.FromResult(data.CatalogAvailable);
    public Task<IReadOnlyList<PrintCatalogItem>> List(bool all)=>Task.FromResult<IReadOnlyList<PrintCatalogItem>>(data.CatalogItems.Values.Where(i=>all||i.Active).OrderBy(i=>i.Kind).ThenBy(i=>i.DisplayOrder).ThenBy(i=>i.Code).ToList());
    public Task<PrintCatalogItem?> Lock(string id)=>Task.FromResult(data.CatalogItems.GetValueOrDefault(id));
    public Task LockKind(string kind)=>Task.CompletedTask;
    public Task<bool> CodeExists(string kind,string code,string? except)=>Task.FromResult(data.CatalogItems.Values.Any(i=>i.Kind==kind&&i.Code==code&&i.Id!=except));
    public Task ClearDefault(string kind,string except,string now){foreach(var item in data.CatalogItems.Values.Where(i=>i.Kind==kind&&i.Id!=except&&i.IsDefault).ToArray())data.CatalogItems[item.Id]=item with{IsDefault=false,Version=item.Version+1,UpdatedAt=now};return Task.CompletedTask;}
    public Task<bool> IsReferenced(PrintCatalogItem item)=>Task.FromResult(data.Orders.Values.Any(o=>o["spec"]?.ToString()?.Contains(item.Id)==true));
    public Task<bool> Delete(string id,int version)=>Task.FromResult(data.CatalogItems.TryGetValue(id,out var item) && item.Version==version && !item.IsDefault && data.CatalogItems.Remove(id));
    public Task<bool> Save(PrintCatalogItem item,int? expected){if(expected!=null&&data.CatalogItems.GetValueOrDefault(item.Id)?.Version!=expected)return Task.FromResult(false);data.CatalogItems[item.Id]=item with{Metadata=item.Metadata.DeepClone().AsObject()};return Task.FromResult(true);}
}

internal sealed class MemoryMagazineRepository(MemoryApiData data) : IMagazineRepository
{
    private static MagazineCard Card(MagazineArticle a)=>new(a.Id,a.Slug,a.Title,a.Excerpt,a.Category,a.Status,a.CoverAssetId==null?null:"/api/banner-assets/"+a.CoverAssetId,a.CoverAlt,a.AuthorName,a.PublishedAt,a.UpdatedAt,a.Version);
    public Task<bool> Available()=>Task.FromResult(data.MagazineAvailable);
    public Task<MagazinePage> List(bool admin,string query,string status,string category,int page,int size, bool oldestFirst = false)
    {
        var visible=data.MagazineArticles.Values.Where(a=>admin||a.Status=="published").ToArray();var filtered=visible.Where(a=>(query==""||a.Title.Contains(query)||a.Excerpt.Contains(query))&&(status==""||a.Status==status)&&(category==""||a.Category==category)).OrderByDescending(a=>a.PublishedAt).ThenByDescending(a=>a.UpdatedAt).ThenBy(a=>a.Id).ToArray();
        return Task.FromResult(new MagazinePage((oldestFirst ? filtered.AsEnumerable().Reverse() : filtered).Skip((page-1)*size).Take(size).Select(Card).ToArray(),filtered.LongLength,page,size,visible.Select(a=>a.Category).Where(c=>c!="").Distinct().OrderBy(c=>c).ToArray()));
    }
    public Task<MagazineLookup?> PublicArticle(string slug){var a=data.MagazineArticles.Values.SingleOrDefault(a=>a.Slug==slug&&a.Status=="published");var alias=false;if(a==null&&data.MagazineAliases.TryGetValue(slug,out var id)){a=data.MagazineArticles.GetValueOrDefault(id);alias=true;}return Task.FromResult(a?.Status=="published"?new MagazineLookup(a,alias):null);}
    public Task<MagazineArticle?> Find(string id)=>Task.FromResult(data.MagazineArticles.GetValueOrDefault(id));
    public Task LockSlugs()=>Task.CompletedTask;
    public Task<bool> SlugUsed(string slug,string except)=>Task.FromResult(data.MagazineArticles.Values.Any(a=>a.Slug==slug&&a.Id!=except)||data.MagazineAliases.TryGetValue(slug,out var id)&&id!=except);
    public Task<bool> AssetExists(string id)=>Task.FromResult(data.AssetMetadata.ContainsKey(id));
    public Task<bool> Save(MagazineArticle a,int? expected,string? previous)
    {
        if(data.FailNextMagazineSave){data.FailNextMagazineSave=false;throw new ChapGiti.Application.Common.UseCaseException(ChapGiti.Application.Common.ErrorKind.Conflict,"Isolated magazine failure.");}
        if(expected!=null&&data.MagazineArticles.GetValueOrDefault(a.Id)?.Version!=expected)return Task.FromResult(false);
        data.MagazineArticles[a.Id]=a;if(previous!=null&&previous!=a.Slug)data.MagazineAliases[previous]=a.Id;data.MagazineAliases.Remove(a.Slug);return Task.FromResult(true);
    }
    public Task<IReadOnlyList<MagazineCard>> Related(string id,string category)=>Task.FromResult<IReadOnlyList<MagazineCard>>(data.MagazineArticles.Values.Where(a=>a.Id!=id&&a.Category==category&&a.Status=="published").Take(3).Select(Card).ToArray());
    public Task<IReadOnlyList<MagazineSitemapItem>> Sitemap()=>Task.FromResult<IReadOnlyList<MagazineSitemapItem>>(data.MagazineArticles.Values.Where(a=>a.Status=="published").Select(a=>new MagazineSitemapItem(a.Slug,a.UpdatedAt)).ToArray());
}
