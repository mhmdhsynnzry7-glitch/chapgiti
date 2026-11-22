using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Orders;
namespace ChapGiti.AuthenticationTests;

internal static class WorkflowSupportScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        using var client=new HttpClient(new HttpClientHandler{UseCookies=false}){BaseAddress=new Uri(host.Origin)};
        var cookies=new Dictionary<string,string>();var data=host.Data;var f=host.Fixture;
        async Task<JsonNode> Request(string method,string path,object? body=null,int expected=200,bool counted=true) {
            using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+path);
            request.Headers.Add("Origin",host.Origin);
            if(cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",cookies.Select(c=>c.Key+"="+c.Value)));
            if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
            if((int)response.StatusCode!=expected)throw new InvalidOperationException(path+" expected "+expected+" got "+(int)response.StatusCode+": "+text);
            if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values){var pair=value.Split(';')[0].Split('=',2);cookies[pair[0]]=pair[1];}
            if(counted)check(true,"workflow/support HTTP "+method+" "+path+" -> "+expected);
            return JsonNode.Parse(text)!;
        }
        await Request("GET","admin/activity",expected:401);
        await Request("POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        await Request("GET","admin/activity",expected:401);
        await Request("POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var owner=f.Store.Accounts[AuthFixture.Phone].Id;
        string Seed(string state="new",bool only=false,long? final=null) {
            var id=Guid.NewGuid().ToString();var now=DateTimeOffset.UtcNow.ToString("O");
            data.Orders[id]=new(){["id"]=id,["code"]=Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),["customer_id"]=owner,["customer"]="Test Customer",["phone"]=AuthFixture.Phone,["email"]="",["title"]="Workflow book",["service"]="book-printing",["spec"]="{\"pages\":120,\"quantity\":1,\"details\":{\"printOnly\":"+(only?"true":"false")+"}}",["status"]=state,["estimate"]=440000L,["final_price"]=final,["admin_notes"]="private legacy note",["version"]=1,["created_at"]=now,["updated_at"]=now};
            data.Events.Add(new(){["id"]=Guid.NewGuid().ToString(),["order_id"]=id,["status"]="new",["note"]="Registered",["actor"]="customer",["created_at"]=now});return id;
        }
        long Version(string id)=>data.Orders[id].Num("version");
        Task<JsonNode> Move(string id,string status,long? price=null,string note="",int expected=200)=>Request("PATCH","admin/orders/"+id,new{status,finalPrice=price,version=Version(id),note},expected);
        var id=Seed();
        await Request("GET","admin/orders/"+id);
        check(data.Orders[id].Str("status")=="new"&&data.Orders[id]["final_price"]==null,"read-only admin GET and suggested estimate do not approve or open an order");
        var opened=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Request("POST","admin/orders/"+id+"/open",new{},counted:false)));
        check(opened.All(r=>r["order"]!["status"]!.ToString()=="review")&&Version(id)==2&&data.Events.Count(e=>e.Str("order_id")==id&&e.Str("status")=="review")==1,"concurrent explicit openings record review exactly once");
        check(data.Orders[id]["final_price"]==null&&data.Events.Single(e=>e.Str("order_id")==id&&e.Str("status")=="review").Str("actor")==AuthFixture.AdminEmail,"opening records actor/time without persisting a suggested price");
        await Request("POST","admin/orders/"+id+"/open",new{});
        check(Version(id)==2,"reopening is idempotent");
        await Move(id,"printing",expected:400);await Move(id,"cancelled",expected:400);
        await Request("PATCH","admin/orders/"+id,new{status="printing",finalPrice="440,000",version=Version(id)},400);
        await Request("PATCH","admin/orders/"+id,new{status="printing",finalPrice=440000,version=1},409);
        await Move(id,"printing",440000,expected:409);
        check(data.Orders[id].Str("status")=="review","legacy administrator update requires all approvals");
        var inProduction=Seed("printing",final:440000);
        await Move(inProduction,"ready",440000,expected:400);await Move(inProduction,"binding",450000,expected:409);
        await Move(inProduction,"binding",440000);await Move(inProduction,"ready",440000);await Move(inProduction,"completed",440000);
        var plain=Seed("printing",only:true,final:123);
        await Move(plain,"binding",123,expected:400);await Move(plain,"ready",123);await Move(plain,"completed",123);
        check(!OrderWorkflow.RequiresBinding(new JsonObject{["details"]=new JsonObject{["printOnly"]=true}})&&OrderWorkflow.RequiresBinding(new()),"binding requirements come from persisted print-only specification with a conservative legacy fallback");
        var rejected=Seed("review");await Move(rejected,"cancelled",note:"فایل قابل چاپ نیست");await Move(rejected,"printing",123,expected:409);
        check(data.Events.Any(e=>e.Str("order_id")==rejected&&e.Str("note").Contains("فایل قابل چاپ نیست")),"rejection is terminal and retains an explicit customer-visible reason");
        var legacy=Seed("confirmed",final:77);await Move(legacy,"printing",77,expected:409);
        var missingPrice=Seed("confirmed");await Move(missingPrice,"printing",expected:400);await Move(missingPrice,"printing",88,expected:409);
        check(data.Orders[legacy].Num("final_price")==77&&data.Orders[missingPrice]["final_price"]==null,"historical confirmed orders preserve existing prices and require an explicit missing price");
        var rollback=Seed();data.FailNextEvent=true;await Request("POST","admin/orders/"+rollback+"/open",new{},500);
        check(data.Orders[rollback].Str("status")=="new"&&Version(rollback)==1,"review update rolls back if event persistence fails");
        var noteId=Guid.NewGuid().ToString();var noteBody=new{idempotencyKey=noteId,note="private new operational note"};
        await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Request("POST","admin/orders/"+id+"/notes",noteBody,counted:false)));
        check(data.Notes.Count==1&&data.Orders[id].Str("admin_notes")=="private legacy note","private note append is idempotent and preserves legacy text");
        await Request("POST","admin/orders/"+id+"/notes",new{idempotencyKey=noteId,note="different"},409);
        var internalDetail=await Request("GET","admin/orders/"+id);
        check(internalDetail["internalNotes"]![0]!["actor"]!=null&&internalDetail["internalNotes"]![0]!["created_at"]!=null,"operational note history has actual actor and timestamp");
        var customerDetail=await Request("GET","customer/orders/"+id);
        var publicDetail=await Request("POST","track",new{code=data.Orders[id].Str("code"),phone=AuthFixture.Phone});
        check(!customerDetail.ToJsonString().Contains("private")&&!publicDetail.ToJsonString().Contains("private"),"private note history and legacy notes never leak through owned or public tracking");
        data.OrderNotesAvailable=false;var unavailable=await Request("POST","admin/orders/"+id+"/notes",new{idempotencyKey=Guid.NewGuid().ToString(),note="unavailable"},503);
        check(unavailable["code"]!.ToString()=="ORDER_NOTES_UNAVAILABLE","pre-migration notes fail explicitly without schema or data mutation");data.OrderNotesAvailable=true;

        var general=WorkflowTestData.Ticket(data,owner);
        var linkedResult=await Request("POST","tickets",new{idempotencyKey=Guid.NewGuid().ToString(),orderId=id,subject="Order question",message="Initial question about my print order."},201);
        var linked=linkedResult["id"]!.ToString();
        var stranger=WorkflowTestData.Ticket(data,Guid.NewGuid().ToString());
        var attachmentId=Guid.NewGuid().ToString();
        data.Files[attachmentId]=new(){["id"]=attachmentId,["order_id"]=id,["name"]="book.pdf",["size"]=128L,["created_at"]=DateTimeOffset.UtcNow.ToString("O"),["storage_key"]="storage-only"};
        var ticketDetail=await Request("GET","tickets/"+linked);
        check(ticketDetail["ticket"]!["order_code"]!.ToString()==data.Orders[id].Str("code"),"owned order-linked conversations expose the order reference");
        var attachments=ticketDetail["orderFiles"]!.AsArray();
        check(attachments.Any(file=>file!["id"]!.ToString()==attachmentId) && attachments.All(file=>file!["id"]!=null && file["name"]!=null && file["storage_key"]==null),"order-linked support exposes only authorized private-file metadata");
        await Request("GET","tickets/"+stranger,expected:404);
        var beforeTickets=data.Tickets.Count;var beforeMessages=data.MessagesById.Count;
        foreach(var path in new[]{"tickets","contact"}) {
            var result=await Request("POST",path,new{idempotencyKey=Guid.NewGuid().ToString(),orderId=id,subject="Order question",message="Keep this active conversation"});
            check(result["id"]!.ToString()==linked,"order support reuses active conversation");
        }
        await Request("POST","tickets/"+linked+"/messages",new{message="Customer reply",idempotencyKey=Guid.NewGuid().ToString()},201);
        await Request("POST","admin/tickets/"+linked+"/messages",new{message="Admin reply",idempotencyKey=Guid.NewGuid().ToString()},201);
        await Request("PATCH","admin/tickets/"+linked,new{status="closed"});
        await Request("PATCH","admin/tickets/"+linked,new{status="open"});
        check(data.Tickets.Count==beforeTickets&&data.MessagesById.Count==beforeMessages+4,"conversation reuse preserves all messages");
        var inbox=await Request("GET","admin/tickets?page=1&pageSize=1&q=Order&status=open");
        check(inbox["total"]!.GetValue<int>()==1&&inbox["items"]!.AsArray().Count==1,"archive search and pagination remain functional");
        var owned=await Request("GET","tickets?page=1&pageSize=1");
        check(owned["total"]!.GetValue<int>()==2&&owned["items"]!.AsArray().Count==1,"customer archive excludes foreign conversations");
        var generalResult=await Request("POST","tickets",new{idempotencyKey=Guid.NewGuid().ToString(),subject="General question",message="A general question about print services."},201);
        var newGeneral=generalResult["id"]!.ToString();
        await Request("POST","admin/tickets/"+newGeneral+"/messages",new{message="We can help with that question.",idempotencyKey=Guid.NewGuid().ToString()},201);
        var conversation=await Request("GET","tickets/"+newGeneral);
        check(conversation["ticket"]!["order_id"]==null && conversation["messages"]!.AsArray().Count==2,"active general support persists both sides without an order");
        async Task<long> Unread(string role)=>(await Request("GET",role+"/notifications/unread-count"))["unreadSupportCount"]!.GetValue<long>();
        var customerUnread=await Unread("customer");var adminUnread=await Unread("admin");
        var incoming=conversation["messages"]!.AsArray().Last()!["id"]!.ToString();
        await Request("POST","tickets/"+newGeneral+"/read",new {throughId=incoming});
        check(await Unread("customer")==customerUnread-1 && await Unread("admin")==adminUnread,"reading a support reply changes only this customer's incoming message count");
        await Request("POST","tickets/"+newGeneral+"/read",new {throughId=incoming});
        check(await Unread("customer")==customerUnread-1,"repeated support read is idempotent across sessions");
        data.OrderLinksAvailable=false;
        var oldTicket=await Request("GET","tickets/"+general);
        check(oldTicket["ticket"]!["order_id"]==null,"historical general tickets still work without the order relationship");
        await Request("GET","admin/tickets?status=invalid",expected:400);await Request("GET","tickets?page=0",expected:400);

        var logins=await Request("GET","admin/activity?type=login");check(logins["total"]!.GetValue<int>()==1,"customer activity excludes administrator sessions");
        await Request("POST","customer/refresh");await Request("POST","customer/logout");
        var after=await Request("GET","admin/activity?type=login");check(after["total"]!.GetValue<int>()==1&&after["items"]![0]!["occurred_at"]!.ToString()==logins["items"]![0]!["occurred_at"]!.ToString(),"refresh and logout do not invent fresh login or historical logout events");
        var unknownActorId=Guid.NewGuid().ToString();
        data.Events.Add(new(){["id"]=unknownActorId,["order_id"]=legacy,["status"]="review",["note"]="Historical event",["actor"]="",["created_at"]=DateTimeOffset.UtcNow.ToString("O")});
        var activity=await Request("GET","admin/activity?pageSize=50");var events=activity["items"]!.AsArray();
        check(events.Single(e=>e!["id"]!.ToString()==unknownActorId)!["actor"]==null,"historical order events with unknown actors are never attributed to the customer");
        check(events.Any(e=>e!["type"]!.ToString()=="ticket_created"&&e["ticket_id"]!.ToString()==linked)&&events.Any(e=>e!["type"]!.ToString()=="order_status"&&e["actor"]?.ToString()=="admin"),"activity contains persisted customer ticket and administrator-attributed order events");
        check(!activity.ToJsonString().Contains("private")&&!activity.ToJsonString().Contains("hash",StringComparison.OrdinalIgnoreCase),"activity projections exclude notes and authentication secrets");
        var normalized=await Request("GET","admin/activity?q="+Uri.EscapeDataString("۰۹۰۰۰۰۰۰۸۲۶")+"&type=login");check(normalized["total"]!.GetValue<int>()==1,"activity search normalizes Persian digits");
        var future=await Request("GET","admin/activity?from=2099-01-01&until=2099-01-02");check(future["total"]!.GetValue<int>()==0,"activity date filters do not fabricate missing history");
        await Request("GET","admin/activity?type=logout",expected:400);await Request("GET","admin/activity?from=2026-12-01&until=2026-01-01",expected:400);
        await Request("GET","admin/activity?pageSize=51",expected:400);
    }
}
