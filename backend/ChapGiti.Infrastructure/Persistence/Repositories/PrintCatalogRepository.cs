using ChapGiti.Application.Catalog;
using ChapGiti.Infrastructure.Persistence.Ef;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class PrintCatalogRepository(SqlStore db) : IPrintCatalogRepository
{
    public async Task<bool> Available() =>
        (await db.One("SELECT CASE WHEN OBJECT_ID('dbo.Print_Catalog_Options','U') IS NULL THEN 0 ELSE 1 END ready"))!
        .Num("ready") == 1;

    private static PrintCatalogItem Map(PrintCatalogOption option) => new(
        option.Id, option.Kind, option.Code, option.Label, option.PricingRule,
        JsonNode.Parse(option.Metadata)!.AsObject(), option.DisplayOrder, option.Active,
        option.IsDefault, option.Version, EfRows.Stamp(option.CreatedAt)!, EfRows.Stamp(option.UpdatedAt)!);

    public async Task<IReadOnlyList<PrintCatalogItem>> List(bool all) =>
        (await db.Context.Set<PrintCatalogOption>()
            .AsNoTracking()
            .Where(option => all || option.Active)
            .OrderBy(option => option.Kind)
            .ThenBy(option => option.DisplayOrder)
            .ThenBy(option => option.Code)
            .ToListAsync())
        .Select(Map).ToList();

    public async Task<PrintCatalogItem?> Lock(string id)
    {
        await db.One("SELECT id FROM dbo.Print_Catalog_Options WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0", id);
        var o = await db.Context.Set<PrintCatalogOption>().AsNoTracking().SingleOrDefaultAsync(o => o.Id == id);
        return o == null ? null : Map(o);
    }
    public async Task LockKind(string kind) => await db.Execute(
        "DECLARE @r int; DECLARE @resource nvarchar(255)=N'ChapGiti.Catalog.'+@p0; " +
        "EXEC @r=sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; " +
        "IF @r<0 THROW 51110,'Catalog is busy.',1;", kind);

    public Task<bool> CodeExists(string kind, string code, string? except) =>
        db.Context.Set<PrintCatalogOption>()
            .AnyAsync(option => option.Kind == kind && option.Code == code && (except == null || option.Id != except));

    public async Task ClearDefault(string kind, string except, string now) =>
        await db.Context.Set<PrintCatalogOption>()
            .Where(option => option.Kind == kind && option.Id != except && option.IsDefault)
            .ExecuteUpdateAsync(set => set
                .SetProperty(option => option.IsDefault, false)
                .SetProperty(option => option.Version, option => option.Version + 1)
                .SetProperty(option => option.UpdatedAt, EfRows.Date(now)));

    public async Task<bool> Delete(string id, int version) =>
        await db.Context.Set<PrintCatalogOption>().Where(o => o.Id == id && o.Version == version && !o.IsDefault).ExecuteDeleteAsync() == 1;

    public async Task<bool> IsReferenced(PrintCatalogItem item) =>
        await db.One("""
            SELECT TOP (1) 1 used FROM dbo.Orders o
            WHERE EXISTS (SELECT 1 FROM OPENJSON(o.spec,'$.catalogSnapshot.options')
              WITH (id varchar(36),kind varchar(40),code varchar(80)) c
              WHERE c.id=@p0 OR (c.kind=@p1 AND c.code=@p2))
            OR (JSON_QUERY(o.spec,'$.catalogSnapshot') IS NULL AND (
              @p2 IN (JSON_VALUE(o.spec,'$.size'),JSON_VALUE(o.spec,'$.paper'),JSON_VALUE(o.spec,'$.binding'),
                       JSON_VALUE(o.spec,'$.details.printType'),JSON_VALUE(o.spec,'$.details.bwPaper'),JSON_VALUE(o.spec,'$.details.colorPaper'),
                       JSON_VALUE(o.spec,'$.details.bindingStyle'),JSON_VALUE(o.spec,'$.details.coverPrint'),JSON_VALUE(o.spec,'$.details.coverPaper'),JSON_VALUE(o.spec,'$.details.coating'))
              OR EXISTS (SELECT 1 FROM OPENJSON(o.spec,'$.details.extras') WHERE value=@p2)))
            """, item.Id, item.Kind, item.Code) != null;

    public async Task<bool> Save(PrintCatalogItem item, int? expected)
    {
        if (expected == null)
        {
            await db.Insert(new PrintCatalogOption
            {
                Id = item.Id,
                Kind = item.Kind,
                Code = item.Code,
                Label = item.Label,
                PricingRule = item.PricingRule,
                Metadata = item.Metadata.ToJsonString(),
                DisplayOrder = item.DisplayOrder,
                Active = item.Active,
                IsDefault = item.IsDefault,
                Version = item.Version,
                CreatedAt = EfRows.Date(item.CreatedAt),
                UpdatedAt = EfRows.Date(item.UpdatedAt)
            });
            return true;
        }
        var json = item.Metadata.ToJsonString();
        return await db.Context.Set<PrintCatalogOption>()
            .Where(option => option.Id == item.Id && option.Version == expected)
            .ExecuteUpdateAsync(set => set
                .SetProperty(option => option.Label, item.Label)
                .SetProperty(option => option.PricingRule, item.PricingRule)
                .SetProperty(option => option.Metadata, json)
                .SetProperty(option => option.DisplayOrder, item.DisplayOrder)
                .SetProperty(option => option.Active, item.Active)
                .SetProperty(option => option.IsDefault, item.IsDefault)
                .SetProperty(option => option.Version, item.Version)
                .SetProperty(option => option.UpdatedAt, EfRows.Date(item.UpdatedAt))) == 1;
    }
}
