using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Domain.Common;
using ChapGiti.Domain.Printing;

namespace ChapGiti.AuthenticationTests;

internal static class BusinessScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host = await TestApiHost.Start();
        using var client = new HttpClient(new HttpClientHandler { UseCookies=false }) { BaseAddress=new Uri(host.Origin) };
        var cookies = new Dictionary<string,string>();
        async Task<JsonNode> Request(string method,string path,object? body=null,int expected=200)
        {
            using var message=new HttpRequestMessage(new HttpMethod(method),"/api/"+path);
            message.Headers.Add("Origin",host.Origin);
            if(cookies.Count>0)message.Headers.Add("Cookie",string.Join("; ",cookies.Select(c=>c.Key+"="+c.Value)));
            if(body!=null)message.Content=path is "orders" or "/api/orders" ? WorkflowTestData.PrintRequest(body) : JsonContent.Create(body);
            using var response=await client.SendAsync(message);
            var text=await response.Content.ReadAsStringAsync();
            if((int)response.StatusCode!=expected)throw new InvalidOperationException(path+" expected "+expected+" got "+(int)response.StatusCode+": "+text);
            if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values){var pair=value.Split(';')[0].Split('=',2);cookies[pair[0]]=pair[1];}
            check(true,"business API "+method+" "+path+" -> "+expected);
            return JsonNode.Parse(text)!;
        }
        await Request("GET","admin/orders",expected:401);
        await Request("POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var beforeLogin=await Request("GET","admin/users?q=&role=&verified=&page=1&pageSize=20");
        check(beforeLogin["items"]![0]!["lastLoginAt"]==null && beforeLogin["items"]![0]!["loginCount"]!.GetValue<int>()==0,"registered customer is not reported as logged in from an administrator session");
        await Request("POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        var logged=await Request("GET","admin/users?q="+Uri.EscapeDataString("۰۹۰۰۰۰۰۰۸۲۶"));
        check(logged["total"]!.GetValue<int>()==1&&logged["items"]![0]!["lastLoginAt"]!=null&&logged["items"]![0]!["activeSessions"]!.GetValue<int>()==1,"directory normalizes Persian mobile and reports a recorded successful login and valid session");
        var loginAt=logged["items"]![0]!["lastLoginAt"]!.GetValue<long>();
        host.Fixture.Clock.Advance(10);
        await Request("POST","customer/refresh");
        var renewed=await Request("GET","admin/users");
        check(renewed["items"]![0]!["lastLoginAt"]!.GetValue<long>()==loginAt&&renewed["items"]![0]!["loginCount"]!.GetValue<int>()==1,"refresh is not counted as another successful login");

        var spec=JsonNode.Parse("""{"pages":120,"quantity":1,"size":"raghi","paper":"white","color":"bw","binding":"hard","details":{"width":14.5,"height":21.5,"printType":"bw-text","sides":"double","bwPages":120,"colorPages":0,"bwPaper":"white70","colorPaper":"white70","printOnly":false,"bindingStyle":"hard-flat","coverPrint":"front","coverPaper":"coated250","coating":"matte","orientation":"portrait","readingDirection":"rtl","extras":["goldFoil"]}}""")!.AsObject();
        var originalQuote=await Request("POST","quote",spec);
        var orderId=Guid.NewGuid().ToString();
        var receipt=await Request("POST","orders",new{idempotencyKey=orderId,title="Business regression book",spec},201);
        check(host.Data.Orders[orderId]["status"]!.ToString()=="new"&&host.Data.Events.Any(e=>e["order_id"]!.ToString()==orderId),"order submission commits a new order and initial history in the isolated repository");
        var newOrders=await Request("GET","admin/orders?q=&status=new&page=1");
        check(newOrders["orders"]![0]!["code"]!.ToString()==receipt["code"]!.ToString(),"a newly committed order is immediately in the administrator new-orders response");
        var search=await Request("GET","admin/orders?q="+receipt["code"]!.ToString().ToLowerInvariant());
        check(search["total"]!.GetValue<int>()==1,"administrator finds committed order by case-insensitive tracking-code search");
        var history=host.Data.Orders[orderId]["spec"]!.ToString();
        var settings=await Request("GET","admin/settings");
        var originalRates=settings["rates"]!.DeepClone();
        check(!CutTariff.Keys.Any(originalRates.AsObject().ContainsKey),"settings expose only cut-independent finishing rates");
        var selectedCut=host.Data.CatalogItems.Values.Single(c=>c.Kind=="bookSizes" && c.Code=="raghi");
        var scoped=(await Request("GET","admin/print-tariffs/"+selectedCut.Id))["rates"]!.DeepClone().AsObject();
        foreach (var entry in originalRates.AsObject()) scoped[entry.Key]=entry.Value?.DeepClone();
        settings["rates"]!["goldFoil"]=17000;
        await Request("PUT","admin/settings",settings);
        var updatedQuote=await Request("POST","quote",spec);
        check(Input.Numeric(updatedQuote["total"])-Input.Numeric(originalQuote["total"])==1000,"explicit finishing tariff update affects subsequent authoritative quotes with existing per-copy formula");
        check(host.Data.Orders[orderId]["spec"]!.ToString()==history,"tariff updates do not recalculate historical order snapshots");
        var saved=await Request("GET","admin/settings");
        check(originalRates.AsObject().Where(p=>p.Key!="goldFoil").All(p=>JsonNode.DeepEquals(p.Value,saved["rates"]![p.Key])),"editing one finishing tariff preserves all other effective prices");
        foreach(var invalid in new JsonNode?[]{null,JsonValue.Create(-1),JsonValue.Create(10000001),JsonValue.Create(1.5),JsonValue.Create("oops")})
        {
            var bad=settings.DeepClone();bad["rates"]!["goldFoil"]=invalid;
            var error=await Request("PUT","admin/settings",bad,400);
            check(error["code"]!.ToString()=="TARIFF_INVALID","invalid finishing tariffs return a structured Persian validation error");
        }
        check(JsonNode.DeepEquals(saved["rates"],(await Request("GET","admin/settings"))["rates"]),"invalid tariff requests preserve saved settings");

        var legacyEdit=settings.DeepClone();legacyEdit["rates"]!["bw"]=4321;
        var legacyFailure=await Request("PUT","admin/settings",legacyEdit,409);
        check(legacyFailure["code"]!.ToString()=="CUT_TARIFF_SCOPE_REQUIRED","legacy clients cannot silently overwrite an unassigned printing tariff");
        var finishes=new JsonObject(settings["rates"]!.AsObject().Where(p=>!ChapGiti.Domain.Printing.CutTariff.Keys.Contains(p.Key)).Select(p=>KeyValuePair.Create(p.Key,p.Value?.DeepClone())));
        await Request("PUT","admin/settings",new {finishingRates=finishes});
        check(JsonNode.DeepEquals(saved["site"],(await Request("GET","admin/settings"))["site"]),"saving finishing alone preserves the site settings");
        var states=new[]{"new","review","confirmed","printing","binding","ready","completed","cancelled"};
        for(var i=0;i<26;i++){
            var row=host.Data.Orders[orderId].ToDictionary();var id=Guid.NewGuid().ToString();
            row["id"]=id;row["status"]=states[i%states.Length];row["code"]=i.ToString("D20");
            row["created_at"]=DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMinutes(i).ToString("O");
            host.Data.Orders[id]=row;
        }
        var first=await Request("GET","admin/orders?page=1");var second=await Request("GET","admin/orders?page=2");
        check(first["total"]!.GetValue<int>()==27&&first["orders"]!.AsArray().Count==20&&second["orders"]!.AsArray().Count==7,"admin pagination reaches all statuses beyond page one");
        check(first["orders"]![0]!["id"]!.ToString()==orderId,"newest committed order sorts first");
        foreach(var status in states){var result=await Request("GET","admin/orders?status="+status);check(result["orders"]!.AsArray().All(r=>r!["status"]!.ToString()==status)&&result["total"]!.GetValue<int>()==host.Data.Orders.Values.Count(r=>r["status"]!.ToString()==status),"matching list and count filters for "+status);}
        await Request("GET","admin/orders?status=unknown",expected:400);
        var fileId=Guid.NewGuid().ToString();
        host.Data.Files[fileId]=new(){["id"]=fileId,["order_id"]=orderId,["name"]="manuscript.pdf",["size"]=100,["object_key"]="private-key",["created_at"]="2026-01-01T00:00:00Z"};
        var detail=await Request("GET","customer/orders/"+orderId);
        check(detail["specification"]!["details"]!=null&&detail["files"]!.AsArray().Count==2&&!detail.ToJsonString().Contains("private-key")&&!detail.ToJsonString().Contains("priceBreakdown"),"owned detail includes saved specifications and safe file metadata, never storage keys or price breakdown");
        await Request("POST","customer/logout");
        await Request("GET","customer/orders/"+orderId,expected:401);
        var signedOut=await Request("GET","admin/users");
        check(signedOut["items"]![0]!["activeSessions"]!.GetValue<int>()==0&&signedOut["items"]![0]!["lastLoginAt"]!.GetValue<long>()==loginAt,"logout retains login evidence but removes valid-session indication");
        check(!signedOut.ToJsonString().Contains("password",StringComparison.OrdinalIgnoreCase)&&!signedOut.ToJsonString().Contains("hash",StringComparison.OrdinalIgnoreCase),"customer directory never exposes authentication secrets");

        var pricing=new PrintPricing(new PrintCatalog(host.Data.Root["options"]!.AsObject()));
        foreach(var service in host.Data.Root["options"]!["finishingServices"]!.AsObject()){
            spec["details"]!["extras"]=new JsonArray(service.Key);
            var validated=pricing.Validate(spec);
            check(Input.Numeric(pricing.Quote(validated,scoped)["extras"])==Input.Numeric(originalRates[service.Key]),"finishing service available and priced per book: "+service.Key);
            foreach(var condition in new[]{"none","printOnly","plastic","soft","gloss"}){
                var incompatible=spec.DeepClone().AsObject();var d=incompatible["details"]!;
                var rejected=condition is "none" or "printOnly" || (condition is "plastic" or "gloss" && service.Value!["matte"]!.GetValue<bool>()) || (condition=="soft"&&service.Value!["hard"]!.GetValue<bool>());
                if(condition=="none")d["coverPrint"]="none";if(condition=="plastic")d["coverPrint"]="plastic";if(condition=="printOnly")d["printOnly"]=true;
                if(condition=="gloss")d["coating"]="gloss";if(condition=="soft"){d["bindingStyle"]="hot-glue";incompatible["binding"]="soft";}
                try{pricing.Validate(incompatible);check(!rejected,"supported finishing combination "+service.Key+"/"+condition);}
                catch(RuleViolation){check(rejected,"incompatible finishing combination rejected "+service.Key+"/"+condition);}
            }
        }
    }
}
