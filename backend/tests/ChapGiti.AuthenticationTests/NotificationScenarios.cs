using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Application.Notifications;
using ChapGiti.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace ChapGiti.AuthenticationTests;

internal static class NotificationScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host = await TestApiHost.Start(); var data = host.Data;
        using var customer = new HttpClient { BaseAddress = new(host.Origin) };
        using var admin = new HttpClient { BaseAddress = new(host.Origin) };
        using var other = new HttpClient { BaseAddress = new(host.Origin) };
        using var guest = new HttpClient { BaseAddress = new(host.Origin) };
        async Task<JsonNode> Request(HttpClient client, string method, string path, object? body = null, int expected = 200)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "/api/" + path);
            request.Headers.Add("Origin", host.Origin); if (body != null) request.Content = path is "orders" or "/api/orders" ? WorkflowTestData.PrintRequest(body) : JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            check((int)response.StatusCode == expected, $"notifications {method} {path} -> {expected} (actual {(int)response.StatusCode})");
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }
        await Request(customer,"POST","customer/login",new {phone=AuthFixture.Phone,password=AuthFixture.Password});
        await Request(admin,"POST","auth/login",new {username="admin",password=AuthFixture.Password});
        var otherId=Guid.NewGuid().ToString(); const string otherPhone="09177777777";
        host.Fixture.Store.Profiles[otherId]=new(otherId,otherPhone,"Other","Owner",true);
        host.Fixture.Store.Accounts[otherPhone]=(otherId,host.Fixture.Store.Accounts[AuthFixture.Phone].Hash);
        await Request(other,"POST","customer/login",new {phone=otherPhone,password=AuthFixture.Password});
        foreach(var context in new[]{"customer","admin"})
        {
            await Request(guest,"GET",context+"/notifications",expected:401);
            await Request(guest,"GET",context+"/notifications/unread-count",expected:401);
            await Request(guest,"POST",context+"/notifications/read-all",new{},401);
            await Request(context=="admin"?customer:admin,"GET",context+"/notifications",expected:401);
        }
        object spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        long Version(string id)=>data.Orders[id].Num("version");
        int Count(string type,string? entity=null)=>data.NotificationRows.Values.Count(row=>row.Notice.Item.EventType==type&&(entity==null||row.Notice.Item.EntityId==entity));
        async Task<string> Create()
        {
            var key=Guid.NewGuid().ToString(); var body=new{idempotencyKey=key,title="Notification request",spec};
            var result=await Request(customer,"POST","orders",body,201); await Request(customer,"POST","orders",body);
            var id=result["id"]!.ToString();
            check(Count("order.created",id)==(data.NotificationsAvailable?1:0),"new request and exact retry respect notification availability without duplicates");
            return id;
        }
        var id=await Create();
        var newNotice=data.NotificationRows.Values.Single().Notice;
        check(newNotice.Recipient==new NotificationRecipient("admin",AuthFixture.AdminEmail)&&newNotice.Item.TargetPath=="/admin/orders/"+id,"request recipient is fixed administrator and target is an internal owned entity route");
        await Request(admin,"GET","admin/orders/"+id); await Request(admin,"POST","admin/orders/"+id+"/open",new{});
        check(data.NotificationRows.Count==1,"passive reads and explicit review opening do not invent notifications");
        object Send(string order)=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(order),amountToman=220000,scopeText="Safe quoted scope",deliveryTerms="",exclusionsText=""};
        var send=Send(id); await Request(admin,"POST","admin/orders/"+id+"/quotes",send); await Request(admin,"POST","admin/orders/"+id+"/quotes",send);
        var quote=data.QuoteRows.Values.Single(q=>q.Str("order_id")==id).Str("id");
        check(Count("quote.issued",id)==1,"issuing/retrying an immutable quote creates one owner notification");
        var ownPage=await Request(customer,"GET","customer/notifications"); var foreignPage=await Request(other,"GET","customer/notifications");
        var ownNotice=ownPage["items"]!.AsArray().Single()!;
        check(ownNotice["targetPath"]!.ToString()=="/account/orders/"+id&&foreignPage["total"]!.GetValue<int>()==0,"quote notification only reaches its owner and deep links to current proposal");
        var noticeId=ownNotice["id"]!.ToString();
        await Request(other,"POST","customer/notifications/"+noticeId+"/read",new{},404);
        await Request(admin,"POST","admin/notifications/"+noticeId+"/read",new{},404);
        var count=await Request(customer,"GET","customer/notifications/unread-count");
        check(count["unreadCount"]!.GetValue<int>()==1,"listing and another recipient's rejected read leave unread state intact");
        await Request(customer,"POST","customer/notifications/"+noticeId+"/read",new{});
        var firstRead=data.NotificationRows[noticeId].Notice.Item.ReadAt;
        await Request(customer,"POST","customer/notifications/"+noticeId+"/read",new{});
        check(firstRead!=null&&data.NotificationRows[noticeId].Notice.Item.ReadAt==firstRead,"read-one is persistent and idempotent without changing its timestamp");
        check((await Request(customer,"GET","customer/notifications/unread-count"))["unreadCount"]!.GetValue<int>()==0,"read-one decrements unread count");
        object Decision(string order,string q,string reason="")=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(order),quoteId=q,quoteVersion=data.QuoteRows[q].Num("version"),reason};
        var revision=Decision(id,quote,"Adjust delivery"); await Request(customer,"POST","customer/orders/"+id+"/quote/request-revision",revision);
        await Request(customer,"POST","customer/orders/"+id+"/quote/request-revision",revision);
        check(Count("quote.revision_requested",id)==1,"customer revision notifies administrator once without echo to customer");
        var revised=Send(id);await Request(admin,"POST","admin/orders/"+id+"/quotes",revised);
        quote=data.QuoteRows.Values.Where(q=>q.Str("order_id")==id).OrderBy(q=>q.Num("revision")).Last().Str("id");
        var accept=Decision(id,quote); var before=data.NotificationRows.Count;
        data.FailNextEvent=true; await Request(customer,"POST","customer/orders/"+id+"/quote/accept",accept,500);
        check(data.NotificationRows.Count==before&&data.Orders[id].Str("status")=="awaiting_customer_approval","business persistence failure creates no phantom notification");
        data.FailNextNotification=true;await Request(customer,"POST","customer/orders/"+id+"/quote/accept",accept,409);
        check(data.NotificationRows.Count==before&&data.Orders[id].Str("status")=="awaiting_customer_approval"&&data.QuoteRows[quote].Str("state")=="sent","notification persistence failure rolls back the quote, order, receipt and inserted notification");
        await Task.WhenAll(Request(customer,"POST","customer/orders/"+id+"/quote/accept",accept),Request(customer,"POST","customer/orders/"+id+"/quote/accept",accept));
        check(Count("quote.accepted",id)==1&&data.Orders[id].Str("status")=="confirmed","concurrent exact retries commit accepted state and one administrator notification");
        var fileId = data.Files.Values.First(f=>f.Str("order_id")==id).Str("id");
        await Request(admin,"POST","admin/orders/"+id+"/files/approve",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),fileId});
        var termsId = Guid.NewGuid().ToString();
        await Request(admin,"POST","admin/orders/"+id+"/delivery-terms",new{idempotencyKey=termsId,expectedVersion=Version(id),delivery="Pickup",scope="Printing"});
        await Request(customer,"POST","customer/orders/"+id+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId});
        var start=new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id)};
        await Request(admin,"POST","admin/orders/"+id+"/start-printing",start); await Request(admin,"POST","admin/orders/"+id+"/start-printing",start);
        foreach(var status in new[]{"binding","ready","completed"})
            await Request(admin,"PATCH","admin/orders/"+id,new{idempotencyKey=Guid.NewGuid().ToString(),version=Version(id),status});
        foreach(var status in new[]{"printing","binding","ready","completed"})check(Count("order."+status,id)==1,"one owner notification for committed production status "+status);
        var ended=await Create(); await Request(admin,"POST","admin/orders/"+ended+"/open",new{});
        await Request(admin,"POST","admin/orders/"+ended+"/quotes",Send(ended));
        var endedQuote=data.QuoteRows.Values.Single(q=>q.Str("order_id")==ended).Str("id");
        await Request(customer,"POST","customer/orders/"+ended+"/quote/decline",Decision(ended,endedQuote,"No longer needed"));
        check(Count("quote.declined",ended)==1&&Count("order.rejected",ended)==0,"customer decline notifies administrator without a false rejection echo");
        var info=await Create(); await Request(admin,"POST","admin/orders/"+info+"/open",new{});
        await Request(admin,"POST","admin/orders/"+info+"/request-information",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(info),message="Please correct artwork",reason="Artwork",requiresFile=true});
        var correction = data.CorrectionRows.Values.Single(c => c.OrderId == info);
        var replacement = Guid.NewGuid().ToString();
        var replacementKey = info + "/" + replacement + ".pdf";
        data.Files[replacement] = new(){["id"]=replacement,["order_id"]=info,["object_key"]=replacementKey,["name"]="fixed.pdf",["size"]=WorkflowTestData.Pdf().Length,["created_at"]=DateTime.UtcNow.ToString("O"),["correction_id"]=correction.Id,["file_version"]=2};
        data.Assets[replacementKey] = WorkflowTestData.Pdf();
        await Request(customer,"POST","customer/orders/"+info+"/information",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(info),message="Scope clarified"});
        await Request(admin,"PATCH","admin/orders/"+info,new{idempotencyKey=Guid.NewGuid().ToString(),version=Version(info),status="cancelled",note="Public reason"});
        check(Count("order.information_requested",info)==1&&Count("order.information_provided",info)==1&&Count("order.rejected",info)==1,"information request, customer completion and reasoned rejection notify the opposite audience");
        foreach(var kind in new[]{"general","order","consultation"}) {
            var beforeRetired=data.NotificationRows.Count;
            await Request(customer,"POST","tickets",new{idempotencyKey=Guid.NewGuid().ToString(),subject="Private subject",message="Private message",orderId=kind=="order"?id:"",consultation=kind=="consultation"},201);
            check(data.NotificationRows.Count==beforeRetired+1,"active "+kind+" intake notifies the administrator");
        }
        check(data.NotificationRows.Values.All(row=>!row.Notice.Item.Body.Contains("Private")&&!row.Notice.Item.Body.Contains("220000")&&!row.Notice.Item.Body.Contains("Public reason")),"notification text excludes messages, notes, commercial amount and user-entered content");
        var legacy=await Create();data.Orders[legacy]["workflow_version"]=1;
        await Request(admin,"POST","admin/orders/"+legacy+"/open",new{});
        await Request(admin,"PATCH","admin/orders/"+legacy,new{version=Version(legacy),status="printing",finalPrice=123456},409);
        check(Count("order.printing",legacy)==0,"blocked historical start emits no production notice");
        var customerNotices=data.NotificationRows.Values.Count(row=>row.Notice.Recipient.Kind=="customer");
        await Request(admin,"POST","admin/orders/"+legacy+"/notes",new{idempotencyKey=Guid.NewGuid().ToString(),note="Private execution note"});
        check(customerNotices==data.NotificationRows.Values.Count(row=>row.Notice.Recipient.Kind=="customer"),"private internal notes create no customer notification");
        await Request(customer,"POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var mixedAdmin=await Request(customer,"GET","admin/notifications");var mixedCustomer=await Request(customer,"GET","customer/notifications");
        check(mixedAdmin["items"]!.AsArray().All(row=>row!["targetPath"]!.ToString().StartsWith("/admin/"))&&mixedCustomer["items"]!.AsArray().All(row=>!row!["targetPath"]!.ToString().StartsWith("/admin/")),"both sessions in one browser still resolve separate recipient identities and projections");
        await Request(customer,"GET","customer/notifications?page=0",expected:400);
        await Request(admin,"GET","admin/notifications?pageSize=51",expected:400);
        var page1=await Request(customer,"GET","customer/notifications?page=1&pageSize=2"); var page2=await Request(customer,"GET","customer/notifications?page=2&pageSize=2");
        check(!page1["items"]!.AsArray().Select(x=>x!["id"]!.ToString()).Intersect(page2["items"]!.AsArray().Select(x=>x!["id"]!.ToString())).Any(),"stable notification pagination has no duplicate rows");
        await Request(customer,"POST","customer/notifications/read-all",new{recipientKind="admin",recipientId=AuthFixture.AdminEmail});
        check((await Request(customer,"GET","customer/notifications/unread-count"))["unreadCount"]!.GetValue<int>()==0&&(await Request(admin,"GET","admin/notifications/unread-count"))["unreadCount"]!.GetValue<int>()>0,"read-all ignores forged recipient and does not affect independent admin unread state");
        await Request(customer,"POST","customer/logout"); await Request(customer,"POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        check((await Request(customer,"GET","customer/notifications"))["items"]!.AsArray().All(row=>row!["readAt"]!=null),"read states survive a new authenticated customer session");
        await Request(admin,"PUT","admin/profile/email",new{email="changed-notifications@example.test"});
        check((await Request(admin,"GET","admin/notifications"))["total"]!.GetValue<int>()>0,"administrator profile email changes do not change the notification recipient identity");
        var afterEmail=await Create();await Request(admin,"POST","admin/orders/"+afterEmail+"/open",new{});
        await Request(admin,"POST","admin/orders/"+afterEmail+"/quotes",Send(afterEmail));
        check(data.QuoteRows.Values.Single(q=>q.Str("order_id")==afterEmail).Str("created_by_admin")==AuthFixture.AdminEmail,"proposal FK and new notifications remain attached to the immutable administrator after changing profile email");
        var size=data.NotificationRows.Count;
        using(var scope=host.App.Services.CreateScope())
        {
            var publisher=scope.ServiceProvider.GetRequiredService<NotificationPublisher>();var uow=scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var original=newNotice.DedupeKey.Split(':')[1];
            await uow.Transaction(async()=>{await publisher.Publish(new(NotificationEvent.RequestCreated,original,id,null,true));return true;});
        }
        check(data.NotificationRows.Count==size,"repository deduplication also prevents repeated projection of the same business event");
        foreach(var bad in new[]{"//evil.test", "../tickets", "https://evil.test", Guid.NewGuid().ToString()+"?next=evil"})
        {
            try { NotificationTargets.For(new("customer","owner"),"order",bad); check(false,"unsafe target rejected"); }
            catch(ArgumentException){check(true,"external or malformed notification reference is rejected");}
        }
        data.NotificationsAvailable=false;
        var unavailable=await Request(customer,"GET","customer/notifications/unread-count",expected:503);
        check(unavailable["code"]!.ToString()=="NOTIFICATIONS_UNAVAILABLE","missing 007 is explicit and does not pretend to have zero unread notifications");
        await Create();check(data.NotificationRows.Count==size,"pre-007 business compatibility never attempts notification writes or a migration");
        check(host.Fixture.Sender.Sends==0,"notification tests never invoke a real SMS or push provider");
    }
}
