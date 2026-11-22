using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ChapGiti.AuthenticationTests;

internal static class CmsVisibilityScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        using var client=new HttpClient(new HttpClientHandler{UseCookies=true}){BaseAddress=new Uri(host.Origin)};
        client.DefaultRequestHeaders.Add("Origin",host.Origin);
        await client.PostAsJsonAsync("/api/auth/login",new{username="admin",password=AuthFixture.Password});
        var banners=await client.GetFromJsonAsync<JsonArray>("/api/admin/banners");
        var portfolio=await client.GetFromJsonAsync<JsonArray>("/api/admin/portfolio");
        var legacy=banners![0]!.DeepClone().AsObject();legacy["id"]="legacy-magazine-link";legacy["link"]="/blog/old-guide";legacy["label"]="Old magazine";banners.Add(legacy);
        using var response=await client.PutAsJsonAsync("/api/admin/banners",banners);check(response.IsSuccessStatusCode,"legacy banner records remain accepted by the compatible private API");
        var persisted=host.Data.Settings["banners"];
        var bootstrap=await client.GetFromJsonAsync<JsonObject>("/api/public/bootstrap");
        check(bootstrap!["articles"]!.AsArray().Count==0&&bootstrap["portfolio"]!.AsArray().Count==0,"public bootstrap keeps legacy array keys empty without advertising retired modules");
        check(bootstrap["banners"]!.AsArray().All(b=>b!["kind"]!.ToString()!="portfolio"),"portfolio promotions do not appear in the public carousel");
        var sanitized=bootstrap["banners"]!.AsArray().Single(b=>b!["id"]!.ToString()=="legacy-magazine-link")!;
        check(sanitized["label"]!.ToString()=="Old magazine"&&sanitized["link"]!.ToString()=="/blog/old-guide","restored magazine destinations remain usable without rewriting historical banners");
        check(host.Data.Settings["banners"]==persisted,"public filtering never rewrites historical banner JSON");
        var retained=await client.GetFromJsonAsync<JsonArray>("/api/admin/portfolio");
        check(JsonNode.DeepEquals(portfolio,retained),"historical portfolio records and image references remain unchanged");
        check(bootstrap["services"]!.AsArray().Count==6&&bootstrap["rates"]==null,"public services remain available without exposing tariff data");
    }
}
