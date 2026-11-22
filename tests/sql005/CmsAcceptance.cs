using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class CmsAcceptance
{
    public static async Task Run(IsolatedDatabase database, AcceptanceHost host, AcceptanceChecks checks, HttpClient admin)
    {
        var before=await checks.Request(admin,"GET","admin/services");
        var tariffs=(await database.Read("SELECT [value] FROM dbo.Settings WHERE [key]='rates'"))?.Str("value");
        var body=before[0]!.DeepClone().AsObject();body.Remove("id");body.Remove("aliases");
        body["slug"]="sql-cms-service";body["title"]="SQL CMS service";body["active"]=false;
        var saved=await checks.Request(admin,"POST","admin/services",body,201);var id=saved["id"]!.ToString();
        var row=await database.Read("SELECT [value] FROM dbo.Settings WHERE [key]='services'");
        var persisted=JsonNode.Parse(row!.Str("value"))!.AsArray();
        checks.Check(persisted.Count==before.AsArray().Count+1&&persisted.Any(s=>s!["id"]!.ToString()==id),"real SQL services JSON persists create without losing the initial catalog");
        using var guest=host.Client();
        await checks.Request(guest,"GET","public/services/sql-cms-service",expected:404);
        var update=saved["service"]!.DeepClone().AsObject();update.Remove("id");update.Remove("aliases");update["active"]=true;update["slug"]="sql-cms-renamed";
        update["fullDescription"]="A real SQL persisted content body for the CMS acceptance test.";
        await checks.Request(admin,"PUT","admin/services/"+id,update);
        await checks.Request(admin,"PUT","admin/services/"+id,update,409);
        var alias=await checks.Request(guest,"GET","public/services/sql-cms-service");
        checks.Check(alias["slug"]!.ToString()=="sql-cms-renamed"&&alias["fullDescription"]!.ToString()==update["fullDescription"]!.ToString(),"real SQL service edit/publication and canonical alias survive independent API scopes");
        var duplicate=body.DeepClone().AsObject();duplicate["slug"]="sql-cms-service";await checks.Request(admin,"POST","admin/services",duplicate,409);
        var all=(await checks.Request(admin,"GET","admin/services")).AsArray();
        var reordered=all.Reverse().Select(s=>new{id=s!["id"]!.ToString(),version=s["version"]!.GetValue<int>()}).ToArray();
        await checks.Request(admin,"POST","admin/services/reorder",new{items=reordered});
        var ordered=await checks.Request(admin,"GET","admin/services");
        checks.Check(ordered[0]!["id"]!.ToString()==reordered[0].id,"SQL-backed services ordering is authoritative and deterministic");
        var staleSettings=(await database.Read("SELECT [value] FROM dbo.Settings WHERE [key]='services'"))!.Str("value");
        await checks.Request(admin,"POST","admin/services/reorder",new{items=reordered},409);
        checks.Check((await database.Read("SELECT [value] FROM dbo.Settings WHERE [key]='services'"))!.Str("value")==staleSettings,"rejected stale SQL reorder cannot partially overwrite the services JSON");
        var item=ordered.AsArray().Single(s=>s!["id"]!.ToString()==id)!.DeepClone().AsObject();item.Remove("id");item.Remove("aliases");item["active"]=false;
        await checks.Request(admin,"PUT","admin/services/"+id,item);
        await checks.Request(guest,"GET","public/services/sql-cms-service",expected:404);
        checks.Check((await database.Read("SELECT [value] FROM dbo.Settings WHERE [key]='rates'"))?.Str("value")==tariffs,"SQL CMS writes do not modify tariffs");
        var banners=(await checks.Request(admin,"GET","admin/banners")).AsArray();var banner=banners[0]!.DeepClone().AsObject();banner["id"]="sql-no-cta";banner["kind"]="promotion";banner["label"]="";banner["link"]="";banners.Add(banner);
        await checks.Request(admin,"PUT","admin/banners",banners);
        var storedBanners=JsonNode.Parse((await database.Read("SELECT [value] FROM dbo.Settings WHERE [key]='banners'"))!.Str("value"))!.AsArray();
        checks.Check(storedBanners.Single(b=>b!["id"]!.ToString()=="sql-no-cta")!["label"]!.ToString()=="","optional banner CTA is genuinely persisted by the SQL repository");
        var publicContent=await checks.Request(guest,"GET","public/bootstrap");
        checks.Check(publicContent["portfolio"]!.AsArray().Count==0&&publicContent["articles"]!.AsArray().Count==0,"SQL-backed bootstrap never advertises the retired modules");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Articles"))!.Num("n")>=4,"retiring public CMS modules does not delete historical SQL articles");
    }
}
