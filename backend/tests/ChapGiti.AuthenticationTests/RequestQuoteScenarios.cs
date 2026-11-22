using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ChapGiti.AuthenticationTests;

internal static class RequestQuoteScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();var data=host.Data;
        using var customer=new HttpClient(){BaseAddress=new Uri(host.Origin)};
        using var admin=new HttpClient(){BaseAddress=new Uri(host.Origin)};
        using var guest=new HttpClient(){BaseAddress=new Uri(host.Origin)};
        async Task<JsonNode> Req(HttpClient client,string method,string path,object? body=null,int expected=200) {
            using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+path);request.Headers.Add("Origin",host.Origin);
            if(body!=null)request.Content=path is "orders" or "/api/orders" ? WorkflowTestData.PrintRequest(body) : JsonContent.Create(body);
            using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
            check((int)response.StatusCode==expected,$"request/quote {method} {path} -> {expected} (actual {(int)response.StatusCode}): "+(response.IsSuccessStatusCode?"":text));
            return JsonNode.Parse(text)!;
        }
        object spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        await Req(guest,"POST","quote",spec,401);
        await Req(guest,"POST","orders",new{idempotencyKey=Guid.NewGuid().ToString(),title="Anonymous forbidden",spec},401);
        await Req(customer,"POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        await Req(admin,"POST","auth/login",new{username="admin",password=AuthFixture.Password});
        await Req(admin,"POST","quote",spec,401);
        await Req(customer,"POST","quote",spec);
        long Version(string id)=>data.Orders[id].Num("version");
        async Task<string> Create(){var r=await Req(customer,"POST","orders",new{idempotencyKey=Guid.NewGuid().ToString(),title="Proposal test",spec},201);return r["id"]!.ToString();}
        object SendBody(string id,long amount=200000,string? key=null)=>new{idempotencyKey=key??Guid.NewGuid().ToString(),expectedVersion=Version(id),amountToman=amount,scopeText="Print supplied book",deliveryTerms="Collect at print shop",exclusionsText="Shipping excluded"};
        object Decision(string id,string quote,string? key=null,string reason="")=>new{idempotencyKey=key??Guid.NewGuid().ToString(),expectedVersion=Version(id),quoteId=quote,quoteVersion=data.QuoteRows[quote].Num("version"),reason};
        var id=await Create();await Req(admin,"GET","admin/orders/"+id);
        var initialEstimate=data.Orders[id].Num("estimate");var snapshot=data.Orders[id].Str("spec");
        var cut=data.CatalogItems.Values.Single(x=>x.Code=="raghi" && x.Kind=="bookSizes");
        var published=(await Req(admin,"GET","admin/print-tariffs/"+cut.Id))["rates"]!.DeepClone().AsObject();
        published["bw"]=published["bw"]!.GetValue<int>()+22;
        await Req(admin,"PUT","admin/print-tariffs/"+cut.Id,new{version=1,rates=published});
        var repriced=await Req(customer,"POST","quote",spec);
        check(repriced["total"]!.GetValue<long>()!=initialEstimate && data.Orders[id].Str("spec")==snapshot && JsonNode.Parse(snapshot)!["priceBreakdown"]!["tariffSnapshot"]!["version"]!.GetValue<int>()==1,"later negotiated cut prices never rewrite the stored order estimate or rate snapshot");
        check(data.Orders[id].Str("status")=="new","passive GET does not open a new workflow request");
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id)},409);
        await Task.WhenAll(Req(admin,"POST","admin/orders/"+id+"/open",new{}),Req(admin,"POST","admin/orders/"+id+"/open",new{}));
        check(data.Events.Count(e=>e.Str("order_id")==id&&e.Str("status")=="review")==1,"simultaneous explicit opens transition exactly once");
        await Req(customer,"POST","admin/orders/"+id+"/quotes",SendBody(id),401);
        await Req(admin,"POST","admin/orders/"+id+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),scopeText="Missing amount"},400);
        await Req(admin,"PATCH","admin/orders/"+id,new{status="printing",version=Version(id),finalPrice=1,idempotencyKey=Guid.NewGuid().ToString()},409);
        var send=SendBody(id,key:Guid.NewGuid().ToString());await Req(admin,"POST","admin/orders/"+id+"/quotes",send);await Req(admin,"POST","admin/orders/"+id+"/quotes",send);
        var first=data.QuoteRows.Values.Single(q=>q.Str("order_id")==id).Str("id");
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendBody(id,999,key:first),409);
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(id,first,key:first),409);
        check(data.Orders[id].Str("status")=="awaiting_customer_approval"&&data.Orders[id]["final_price"]==null,"sending persists one immutable proposal without approved price or production");
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id)},409);
        await Req(guest,"POST","customer/orders/"+id+"/quote/accept",Decision(id,first),401);
        await Req(admin,"POST","customer/orders/"+id+"/quote/accept",Decision(id,first),401);
        var owner=data.Orders[id]["customer_id"];data.Orders[id]["customer_id"]=Guid.NewGuid().ToString();
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(id,first),404);data.Orders[id]["customer_id"]=owner;
        await Req(customer,"POST","customer/orders/"+id+"/quote/request-revision",Decision(id,first),400);
        await Req(customer,"POST","customer/orders/"+id+"/quote/request-revision",Decision(id,first,reason:"Change delivery terms"));
        check(data.Orders[id].Str("status")=="review"&&data.QuoteRows[first].Str("state")=="revision_requested","revision request reopens review without editing historic amount/terms");
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(id,first),409);
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendBody(id,220000));
        var second=data.QuoteRows.Values.Single(q=>q.Str("order_id")==id&&q.Num("revision")==2).Str("id");
        var stale=Decision(id,second);await Req(admin,"POST","admin/orders/"+id+"/quotes",SendBody(id,230000));
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",stale,409);
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(id,second),409);
        check(data.QuoteRows[second].Str("state")=="superseded"&&data.QuoteRows[first].Num("amount_toman")==200000,"reissue supersedes only the active quote and preserves prior commercial terms");
        var third=data.QuoteRows.Values.Single(q=>q.Str("order_id")==id&&q.Num("revision")==3).Str("id");
        var accept=Decision(id,third,key:Guid.NewGuid().ToString());data.FailNextEvent=true;
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",accept,500);
        check(data.Orders[id]["final_price"]==null&&data.QuoteRows[third].Str("state")=="sent","event persistence failure rolls back acceptance, order price and command receipt together");
        var attempts=await Task.WhenAll(Req(customer,"POST","customer/orders/"+id+"/quote/accept",accept),Req(customer,"POST","customer/orders/"+id+"/quote/accept",accept));
        check(data.Orders[id].Str("status")=="confirmed"&&data.Orders[id].Num("final_price")==230000&&data.QuoteRows[third].Str("state")=="accepted","owner acceptance atomically records price, accepted proposal and queued status");
        var activity=await Req(admin,"GET","admin/activity?type=order_status&page=1&pageSize=50");
        check(activity["items"]!.AsArray().Any(row=>row!["order_id"]?.ToString()==id&&row["status"]?.ToString()=="confirmed"&&row["actor"]?.ToString()==row["customer_name"]?.ToString()),"customer decision activity uses the actual owner's name rather than a technical actor identifier");
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(id,third),409);
        var detail=await Req(customer,"GET","customer/orders/"+id);
        check(!detail.ToJsonString().Contains("created_by_admin")&&!detail.ToJsonString().Contains("admin_notes")&&!detail.ToJsonString().Contains("payload_hash"),"public quote projection excludes administrative identities, notes and command hashes");
        var fileId = data.Files.Values.First(f=>f.Str("order_id")==id).Str("id");
        await Req(admin,"POST","admin/orders/"+id+"/files/approve",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),fileId});
        var termsId = Guid.NewGuid().ToString();
        await Req(admin,"POST","admin/orders/"+id+"/delivery-terms",new{idempotencyKey=termsId,expectedVersion=Version(id),delivery="Pickup",scope="Printing"});
        await Req(customer,"POST","customer/orders/"+id+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId});
        var start=new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id)};await Req(admin,"POST","admin/orders/"+id+"/start-printing",start);await Req(admin,"POST","admin/orders/"+id+"/start-printing",start);
        check(data.Orders[id].Str("status")=="printing","printing starts only on the explicit administrator command after approval");
        foreach(var status in new[]{"binding","ready","completed"})await Req(admin,"PATCH","admin/orders/"+id,new{idempotencyKey=Guid.NewGuid().ToString(),version=Version(id),status});
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id)},409);
        var cancel=await Create();await Req(admin,"POST","admin/orders/"+cancel+"/open",new{});await Req(admin,"POST","admin/orders/"+cancel+"/quotes",SendBody(cancel));
        var cq=data.QuoteRows.Values.Single(q=>q.Str("order_id")==cancel).Str("id");await Req(customer,"POST","customer/orders/"+cancel+"/quote/decline",Decision(cancel,cq,reason:"No longer needed"));
        check(data.Orders[cancel].Str("status")=="cancelled"&&data.Orders[cancel]["final_price"]==null,"customer decline records actor/reason and never approves a final price");
        var info=await Create();await Req(admin,"POST","admin/orders/"+info+"/open",new{});
        await Req(admin,"POST","admin/orders/"+info+"/request-information",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(info),message="Please send artwork",reason="Artwork",requiresFile=true});
        await Req(admin,"POST","admin/orders/"+info+"/quotes",SendBody(info),409);
        await Req(customer,"POST","customer/orders/"+info+"/information",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(info),message="Artwork attached"},409);
        var correction=data.CorrectionRows.Values.Single(c=>c.OrderId==info);
        var fid=Guid.NewGuid().ToString();var fkey=info+"/"+fid+".pdf";
        data.Files[fid]=new(){["id"]=fid,["order_id"]=info,["object_key"]=fkey,["name"]="corrected.pdf",["size"]=WorkflowTestData.Pdf().Length,["created_at"]=Input.Now(),["correction_id"]=correction.Id,["file_version"]=1};
        data.Assets[fkey]=WorkflowTestData.Pdf();
        await Req(customer,"POST","customer/orders/"+info+"/information",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(info),message="Artwork attached"});
        await Req(admin,"POST","admin/orders/"+info+"/quotes",SendBody(info),409);
        await Req(admin,"POST","admin/orders/"+info+"/corrections/review",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(info),revisionId=correction.Id,revisionVersion=2,decision="approve",reason="Reviewed artwork"});
        await Req(admin,"POST","admin/orders/"+info+"/quotes",SendBody(info));
        var grouped=await Req(customer,"GET","customer/orders?page=1&group=decision");check(grouped["total"]!.GetValue<int>()==1,"customer groups count only owned matching request states");
        using(var scope=host.App.Services.CreateScope()) {
            var email=new string('a',64)+"@"+new string('b',63)+"."+new string('c',63)+"."+new string('d',61);
            await scope.ServiceProvider.GetRequiredService<OrderQuoteService>().Send(info,
                System.Text.Json.JsonSerializer.SerializeToNode(SendBody(info))!.AsObject(),new(){["id"]=AuthFixture.AdminEmail,["email"]=email});
            check(email.Length==254&&data.Events.Last(e=>e.Str("order_id")==info).Str("actor")==email,
                "maximum administrator email fits the unchanged Events.actor SQL contract without a namespace prefix or truncation");
        }
        var newestFirst = await Req(admin,"GET","admin/orders?page=1&pageSize=50");
        var oldestPage = await Req(admin,"GET","admin/orders?page=1&pageSize=1&oldestFirst=true");
        var nextOldest = await Req(admin,"GET","admin/orders?page=2&pageSize=1&oldestFirst=true");
        var descending = newestFirst["orders"]!.AsArray();
        check(descending.Count >= 2 && oldestPage["orders"]![0]!["id"]!.ToString()==descending[^1]!["id"]!.ToString() &&
            nextOldest["orders"]![0]!["id"]!.ToString()==descending[^2]!["id"]!.ToString() &&
            oldestPage["total"]!.ToString()==newestFirst["total"]!.ToString(),"server sorts the complete order list before paging without changing the total");
        data.QuotesAvailable=false;
        var legacyRead=await Req(customer,"GET","customer/orders/"+info);check(legacyRead["quotesAvailable"]!.GetValue<bool>()==false,"missing migration preserves readable customer details");
        await Req(customer,"POST","orders",new{idempotencyKey=Guid.NewGuid().ToString(),title="Migration not applied",spec},503);
        var ownerId=host.Fixture.Store.Accounts[AuthFixture.Phone].Id;
        var profile=host.Fixture.Store.Profiles[ownerId];
        host.Fixture.Store.Profiles[ownerId]=profile with { First="" };
        await Req(customer,"POST","quote",spec,401);
        host.Fixture.Store.Profiles[ownerId]=profile;
        host.Fixture.Clock.Advance(901);
        await Req(customer,"POST","quote",spec,401);
        check(host.Fixture.Sender.Sends==0,"request-to-production API tests never invoke SMS");
    }
}
