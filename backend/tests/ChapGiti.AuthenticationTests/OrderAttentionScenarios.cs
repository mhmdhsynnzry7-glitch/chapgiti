using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ChapGiti.Infrastructure.Persistence;
using ChapGiti.Infrastructure.Persistence.Ef;
using ChapGiti.Infrastructure.Persistence.Repositories;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class OrderAttentionScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using (var context=new ChapGitiDesignFactory().CreateDbContext([]))
        await using (var store=new SqlStore(new ConfigurationBuilder().Build(), context:context))
        {
            var sqlInbox=new OrderInboxRepository(store);
            foreach (var recipient in new[]{"admin","customer"})
            foreach (var filter in new[]{"","new","unread","mine","customer","admin"})
            {
                var query=sqlInbox.Matches(new(new(recipient,"scope-test"),"new,review","CG-",filter,0,10));
                var sql=query.OrderByDescending(x=>x.Fact.CreatedAt).Skip(0).Take(10).ToQueryString();
                check(sql.Contains("dbo.Order_Quotes") && sql.Contains("dbo.Order_Views") && sql.Contains("FETCH NEXT"),"SQL Server translates role-scoped inbox ordering and "+recipient+"/"+filter+" before paging");
            }
        }
        await using var host=await TestApiHost.Start();var data=host.Data;
        using var customer=new HttpClient{BaseAddress=new(host.Origin)};
        using var admin=new HttpClient{BaseAddress=new(host.Origin)};
        using var guest=new HttpClient{BaseAddress=new(host.Origin)};
        using var other=new HttpClient{BaseAddress=new(host.Origin)};
        async Task<JsonNode> Req(HttpClient client,string method,string path,object? body=null,int expected=200)
        {
            using var request=new HttpRequestMessage(new(method),"/api/"+path);request.Headers.Add("Origin",host.Origin);
            if(body!=null)request.Content=path is "orders" or "/api/orders" ? WorkflowTestData.PrintRequest(body) : JsonContent.Create(body);
            using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
            check((int)response.StatusCode==expected,$"order attention {method} {path} -> {expected} (actual {(int)response.StatusCode}): "+(response.IsSuccessStatusCode?"":text));
            return JsonNode.Parse(text)!;
        }
        async Task<(long Notifications,long Orders)> Counts(HttpClient client,string scope){var n=await Req(client,"GET",scope+"/notifications/unread-count");return(n["unreadCount"]!.GetValue<long>(),n["unseenOrderCount"]!.GetValue<long>());}
        await Req(customer,"POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        await Req(admin,"POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var otherId=Guid.NewGuid().ToString();const string otherPhone="09177777777";
        host.Fixture.Store.Profiles[otherId]=new(otherId,otherPhone,"Other","Owner",true);
        host.Fixture.Store.Accounts[otherPhone]=(otherId,host.Fixture.Store.Accounts[AuthFixture.Phone].Hash);
        await Req(other,"POST","customer/login",new{phone=otherPhone,password=AuthFixture.Password});
        object spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        var create=new{idempotencyKey=Guid.NewGuid().ToString(),title="Attention test order",spec};
        data.SimulatedTrackingCollisions=5;
        await Req(customer,"POST","orders",create,503);
        check(data.Orders.Count==0 && data.Files.Count==0,"exhausted tracking collisions do not create a partial order or file");
        data.SimulatedTrackingCollisions=1;
        var id=(await Req(customer,"POST","orders",create,201))["id"]!.ToString();
        var tracking=data.Orders[id].Str("code");
        check(System.Text.RegularExpressions.Regex.IsMatch(tracking,"^CG-[2-9A-HJ-NP-Z]{5}-[2-9A-HJ-NP-Z]{5}$") && data.SimulatedTrackingCollisions==0,"a retried tracking code is short, independent from the internal id and excludes ambiguous characters");
        await Req(customer,"POST","orders",create);
        check(await Counts(admin,"admin")== (1,1)&&await Counts(customer,"customer")== (0,0),"new order creates one administrator unseen order and no fabricated customer attention");
        await Req(admin,"GET","admin/orders");var detail=await Req(admin,"GET","admin/orders/"+id);
        check((await Counts(admin,"admin")).Orders==1&&data.OrderViews.Count==0,"listing and passive details/menu reads never mark orders seen");
        await Req(admin,"POST","admin/notifications/read-all",new{});
        check(await Counts(admin,"admin")== (0,1),"reading all notifications does not mark an order viewed");
        var seen=new{seenThroughSequence=detail["viewSequence"]!.GetValue<long>()};
        await Req(admin,"POST","admin/orders/"+id+"/view",seen);await Req(admin,"POST","admin/orders/"+id+"/view",seen);
        check(await Counts(admin,"admin")== (0,0)&&data.OrderViews.Count==1,"explicit view persists idempotently independent from notification reads");
        var mine=await Req(admin,"GET","admin/orders?attention=mine");var state=mine["orders"]![0]!["attention"]!;
        check(state["required"]!.GetValue<bool>() && !state["unread"]!.GetValue<bool>() && state["code"]!.ToString()=="new","viewing an order clears unread but preserves the administrator's required action");
        check((await Req(admin,"GET","admin/orders?attention=unread"))["total"]!.GetValue<int>()==0,"unread filter is independent of new and action filters");
        var notice=await Req(admin,"GET","admin/notifications");
        check(notice["items"]![0]!["orderCode"]!.ToString()==data.Orders[id].Str("code"),"notification and order use the same customer-safe tracking reference");
        await Req(admin,"POST","admin/orders/"+id+"/open",new{});
        async Task Quote(long amount)=>await Req(admin,"POST","admin/orders/"+id+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=data.Orders[id].Num("version"),amountToman=amount,scopeText="Agreed print scope",deliveryTerms="",exclusionsText=""});
        await Quote(100000);
        check((await Req(customer,"GET","customer/orders?attention=mine"))["items"]![0]!["attention"]!["code"]!.ToString()=="accept-price","latest quote assigns action to its customer");
        check((await Req(other,"GET","customer/orders?attention=mine"))["total"]!.GetValue<int>()==0,"attention filters preserve recipient boundaries");
        var first=await Req(customer,"GET","customer/orders/"+id);var firstSequence=first["viewSequence"]!.GetValue<long>();
        check(await Counts(customer,"customer")== (1,1)&&await Counts(other,"customer")== (0,0),"customer attention counts are scoped to the actual order owner");
        await Req(other,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence},404);
        await Req(guest,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence},401);
        await Req(customer,"POST","admin/orders/"+id+"/view",new{seenThroughSequence=firstSequence},401);
        await Req(admin,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence},401);
        await Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence+100},400);
        await Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=-1},400);
        await Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence="bad"},400);
        await Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence,recipientKind="admin",recipientId=AuthFixture.AdminEmail});
        check(await Counts(customer,"customer")== (1,0)&&await Counts(admin,"admin")== (0,0),"order view ignores forged recipient fields and preserves unread notifications");
        using(var secondSession=new HttpClient{BaseAddress=new(host.Origin)}){
            await Req(secondSession,"POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
            check(await Counts(secondSession,"customer")== (1,0),"viewed state persists in a separate login session");
        }
        await Quote(120000);await Quote(140000);
        check(await Counts(customer,"customer")== (3,1),"multiple new events on one order count as one unseen order");
        await Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence});
        check((await Counts(customer,"customer")).Orders==1,"a stale visible snapshot never acknowledges a newer concurrent event");
        var current=await Req(customer,"GET","customer/orders/"+id);var latest=current["viewSequence"]!.GetValue<long>();
        await Task.WhenAll(Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=latest}),Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=firstSequence}));
        check((await Counts(customer,"customer")).Orders==0,"concurrent view acknowledgements advance monotonically");
        var before=data.OrderViews.ToDictionary();await Req(customer,"POST","customer/notifications/read-all",new{});
        check(before.All(p=>data.OrderViews[p.Key]==p.Value),"notification read-all never changes persisted order-view records");
        var quote=data.QuoteRows.Values.Where(q=>q.Str("order_id")==id).MaxBy(q=>q.Num("revision"))!;
        await Req(customer,"POST","customer/orders/"+id+"/quote/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=data.Orders[id].Num("version"),quoteId=quote.Str("id"),quoteVersion=quote.Num("version")});
        check((await Counts(admin,"admin")).Orders==1&& (await Counts(customer,"customer")).Orders==0,"customer acceptance creates independent administrator order attention");
        data.Settings["order_seen_baseline_sequence"]=data.NotificationSequence.ToString();
        check((await Counts(admin,"admin")).Orders==0,"activation baseline avoids fabricated historical unseen attention");
        data.OrderViewsAvailable=false;var unavailable=await Req(customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=latest},503);
        check(unavailable["code"]!.ToString()=="ORDER_VIEWS_UNAVAILABLE"&& (await Counts(admin,"admin")).Orders==0,"pending view migration degrades only order badges while notifications stay available");
        var historical=WorkflowTestData.Ticket(data,host.Fixture.Store.Accounts[AuthFixture.Phone].Id,id);
        var orderVersion = data.Orders[id].Num("version");
        var messageKey=Guid.NewGuid().ToString();
        await Req(customer,"POST","tickets/"+historical+"/messages",new{message="تأیید است",idempotencyKey=messageKey},201);
        await Req(customer,"POST","tickets/"+historical+"/messages",new{message="تأیید است",idempotencyKey=messageKey});
        var inbox = await Req(admin,"GET","admin/tickets?page=1");
        check(inbox["items"]!.AsArray().First(i=>i!["id"]!.ToString()==historical)!["unread_admin"]!.GetValue<int>()>0,"unread support messages persist for administrator");
        await Req(admin,"POST","admin/tickets/"+historical+"/read",new{throughId=messageKey});
        check(data.MessagesById[messageKey]["read_at"]!=null, "support read acknowledgement is persisted");
        await Req(other,"POST","tickets/"+historical+"/read",new{throughId=messageKey},404);
        await Req(admin,"POST","admin/tickets/"+historical+"/messages",new{message="پاسخ درخواست شما",idempotencyKey=Guid.NewGuid().ToString()},201);
        await Req(admin,"PATCH","admin/tickets/"+historical,new{status="closed"});
        await Req(admin,"PATCH","admin/tickets/"+historical,new{status="open"});
        await Req(guest,"POST","tickets",new{},401);
        check(data.Orders[id].Num("version")==orderVersion,"support acknowledgement, replies and reopen never change the linked order");
        data.MessageReadsAvailable=false;
        await Req(customer,"GET","tickets/"+historical);
        await Req(customer,"POST","tickets/"+historical+"/messages",new{message="pending migration",idempotencyKey=Guid.NewGuid().ToString()},503);
        check(host.Fixture.Sender.Sends==0,"attention/archive API tests never access SQL Server or real SMS");
    }
}
