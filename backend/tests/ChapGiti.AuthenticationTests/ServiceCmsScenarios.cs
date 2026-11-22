using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Features;
using ChapGiti.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ChapGiti.AuthenticationTests;

internal static class ServiceCmsScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        using var admin=new HttpClient(new HttpClientHandler{UseCookies=true}){BaseAddress=new Uri(host.Origin)};
        using var guest=new HttpClient{BaseAddress=new Uri(host.Origin)};
        admin.DefaultRequestHeaders.Add("Origin",host.Origin);guest.DefaultRequestHeaders.Add("Origin",host.Origin);
        async Task<JsonNode> Send(HttpClient client,string method,string route,object? body=null,int status=200){
            using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+route);
            if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);
            check((int)response.StatusCode==status,"services HTTP "+method+" "+route+" -> "+status);
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }
        var original=await Send(guest,"GET","public/services");
        check(original.AsArray().Count==6&&original.AsArray().All(s=>s!["active"]!.GetValue<bool>()),"all six original service contents are available before the first CMS write");
        await Send(guest,"GET","admin/services",status:401);
        await Send(guest,"POST","admin/services",new{},401);
        await Send(admin,"POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var body=original[0]!.DeepClone().AsObject();body.Remove("id");body.Remove("aliases");
        body["slug"]="cms-new-service";body["title"]="CMS new service";body["description"]="An isolated CMS service description.";
        body["displayOrder"]=0;body["active"]=false;
        var ratesBefore=host.Data.Settings.GetValueOrDefault("rates");
        var saved=await Send(admin,"POST","admin/services",body,201);var id=saved["id"]!.ToString();
        check((await Send(admin,"GET","admin/services")).AsArray().Count==7&&host.Data.Settings.ContainsKey("services"),"first CMS create persists the entire preserved catalog under only Settings.services");
        check((await Send(guest,"GET","public/services")).AsArray().Count==6,"draft services never leak into the public list");
        await Send(guest,"GET","public/services/cms-new-service",status:404);
        await Send(admin,"POST","admin/services",body,409);
        var draft=saved["service"]!.DeepClone().AsObject();draft.Remove("id");draft.Remove("aliases");draft["active"]=true;draft["slug"]="cms-renamed-service";
        var updated=await Send(admin,"PUT","admin/services/"+id,draft);
        await Send(admin,"PUT","admin/services/"+id,draft,409);
        var alias=await Send(guest,"GET","public/services/cms-new-service");
        check(alias["slug"]!.ToString()=="cms-renamed-service"&&alias["aliases"]!.AsArray().Any(a=>a!.ToString()=="cms-new-service"),"renaming preserves the old URL as a canonical alias");
        var collision=body.DeepClone().AsObject();collision["slug"]="cms-new-service";await Send(admin,"POST","admin/services",collision,409);
        var current=(await Send(admin,"GET","admin/services")).AsArray();
        var reorder=current.Reverse().Select(s=>new{id=s!["id"]!.ToString(),version=s["version"]!.GetValue<int>()}).ToArray();
        await Send(admin,"POST","admin/services/reorder",new{items=reorder});
        var reordered=await Send(guest,"GET","public/services");
        check(reordered[0]!["id"]!.ToString()==reorder[0].id,"saved display ordering is used in public responses");
        await Send(admin,"POST","admin/services/reorder",new{items=reorder},409);
        foreach(var invalid in new[]{"javascript:alert(1)","//evil.example/image.png","/images/not-found.svg","/api/banner-assets/"+Guid.NewGuid()}){
            var bad=body.DeepClone().AsObject();bad["slug"]="invalid-image";bad["image"]=invalid;await Send(admin,"POST","admin/services",bad,400);
        }
        foreach(var field in new[]{"slug","description","active","displayOrder","features","sections"}){
            var bad=body.DeepClone().AsObject();bad["slug"]="invalid-fields";
            bad[field]=field switch{"slug"=>JsonValue.Create("Bad Slug"),"description"=>JsonValue.Create("short"),"active"=>JsonValue.Create("true"),"displayOrder"=>JsonValue.Create(-1),_=>JsonValue.Create("wrong")};
            await Send(admin,"POST","admin/services",bad,400);
        }
        var settingsBefore=host.Data.Settings["services"];
        host.Data.FailNextSettingSave=true;
        var failing=body.DeepClone().AsObject();failing["slug"]="rolled-back-service";await Send(admin,"POST","admin/services",failing,409);
        check(host.Data.Settings["services"]==settingsBefore&&host.Data.Settings.GetValueOrDefault("rates")==ratesBefore,"failed service save rolls back and never changes tariffs");
        var bootstrap=await Send(guest,"GET","public/bootstrap");
        check(bootstrap["services"]!.AsArray().Count==7,"bootstrap carries the same published catalog for server-rendered consumers");
        check(host.Fixture.Sender.Sends==0,"services CMS tests never send SMS");
    }
}
