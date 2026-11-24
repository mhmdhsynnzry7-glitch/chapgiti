using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderAttentionSqlScenarios
{
    public static async Task Run(OrderWorkflowApi api)
    {
        // Explicitly acknowledge synthetic historical fixtures before measuring new-order counters.
        var existing=(await api.Database.Read("SELECT STRING_AGG(id,',') ids FROM dbo.Orders"))!.Str("ids");
        foreach(var id in existing.Split(',',StringSplitOptions.RemoveEmptyEntries))
        foreach(var (client,role) in new[]{(api.Customer,"customer"),(api.Administrator,"admin")})
        {
            var detail=await api.Request(client,"GET",role+"/orders/"+id);
            await api.Request(client,"POST",role+"/orders/"+id+"/view",new{seenThroughSequence=detail["viewSequence"]!.GetValue<long>()});
        }
        await api.Request(api.Customer,"POST","customer/notifications/read-all",new{});
        await api.Request(api.Administrator,"POST","admin/notifications/read-all",new{});
        var request=OrderWorkflowApi.Creation();
        var idNew=(await api.Request(api.Customer,"POST","orders",request,201))["id"]!.ToString();
        await api.Request(api.Customer,"POST","orders",request);
        api.Checks.Check(await api.Counts(api.Administrator,"admin")== (1,1)&&await api.Counts(api.Customer,"customer")== (0,0),"SQL request retry persists exactly one administrator notice and independent unseen-order badge");
        var views=(await api.Database.Read("SELECT COUNT(*) n FROM dbo.Order_Views"))!.Num("n");
        await api.Request(api.Administrator,"GET","admin/orders");var detailNew=await api.Detail(idNew);
        await api.Request(api.Administrator,"GET","admin/notifications");
        api.Checks.Check((await api.Counts(api.Administrator,"admin")).Orders==1&&(await api.Database.Read("SELECT COUNT(*) n FROM dbo.Order_Views"))!.Num("n")==views,"SQL list, detail, menu summary and notification reads do not acknowledge orders");
        await api.Request(api.Administrator,"POST","admin/notifications/read-all",new{});
        api.Checks.Check(await api.Counts(api.Administrator,"admin")== (0,1),"SQL notification read-all leaves order attention unread");
        var seen=new{seenThroughSequence=detailNew["viewSequence"]!.GetValue<long>()};
        await Task.WhenAll(api.Request(api.Administrator,"POST","admin/orders/"+idNew+"/view",seen),api.Request(api.Administrator,"POST","admin/orders/"+idNew+"/view",seen));
        api.Checks.Check(await api.Counts(api.Administrator,"admin")== (0,0),"SQL duplicate explicit view acknowledgements persist once");
        await api.Request(api.Administrator,"POST","admin/orders/"+idNew+"/open",new{});await api.Quote(idNew);
        var first=await api.CustomerDetail(idNew);var sequence=first["viewSequence"]!.GetValue<long>();
        api.Checks.Check(await api.Counts(api.Customer,"customer")== (1,1)&&await api.Counts(api.Other,"customer")== (0,0),"SQL customer attention and notices belong only to the order owner");
        await api.Request(api.Other,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence},404);
        await api.Request(api.Guest,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence},401);
        await api.Request(api.Customer,"POST","admin/orders/"+idNew+"/view",new{seenThroughSequence=sequence},401);
        await api.Request(api.Administrator,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence},401);
        await api.Request(api.Customer,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence+1000},400);
        await api.Request(api.Customer,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=-1},400);
        await api.Request(api.Customer,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence,recipientKind="admin",recipientId=OrderWorkflowFixtures.Administrator});
        api.Checks.Check(await api.Counts(api.Customer,"customer")== (1,0)&&await api.Counts(api.Administrator,"admin")== (0,0),"SQL order view ignores forged recipient data and does not read the notification");
        using(var second=api.Host.Client())
        {
            await api.Request(second,"POST","customer/login",new{phone=OrderWorkflowFixtures.Phone,password=OrderWorkflowFixtures.Password});
            api.Checks.Check(await api.Counts(second,"customer")== (1,0),"SQL order view and unread notices survive a new authenticated session");
        }
        await api.Quote(idNew,220000);await api.Quote(idNew,230000);
        api.Checks.Check(await api.Counts(api.Customer,"customer")== (3,1),"SQL multiple later quote revisions require attention on one distinct order");
        await api.Request(api.Customer,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence});
        api.Checks.Check((await api.Counts(api.Customer,"customer")).Orders==1,"SQL stale visible snapshot cannot consume a later event");
        var latest=(await api.CustomerDetail(idNew))["viewSequence"]!.GetValue<long>();
        await Task.WhenAll(api.Request(api.Customer,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=latest}),api.Request(api.Customer,"POST","customer/orders/"+idNew+"/view",new{seenThroughSequence=sequence}));
        api.Checks.Check((await api.Counts(api.Customer,"customer")).Orders==0&&(await api.Database.Read("SELECT seen_through_sequence FROM dbo.Order_Views WHERE order_id=@p0 AND customer_id=@p1",idNew,api.Fixtures.Owner))!.Num("seen_through_sequence")==latest,"SQL concurrent view upserts advance monotonically");
        var viewSnapshot=await OrderWorkflowSqlScenarios.JsonSnapshot(api.Database,"SELECT * FROM dbo.Order_Views ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES");
        var notice=(await api.Database.Read("SELECT TOP(1) id FROM dbo.Notifications WHERE customer_id=@p0 AND entity_id=@p1 ORDER BY sequence DESC",api.Fixtures.Owner,idNew))!.Str("id");
        await api.Request(api.Other,"POST","customer/notifications/"+notice+"/read",new{},404);
        await api.Request(api.Administrator,"POST","admin/notifications/"+notice+"/read",new{},404);
        await api.Request(api.Customer,"POST","customer/notifications/"+notice+"/read",new{});await api.Request(api.Customer,"POST","customer/notifications/"+notice+"/read",new{});
        api.Checks.Check(await api.Counts(api.Customer,"customer")== (2,0),"SQL individual notification read is idempotent and role/owner isolated");
        await api.Request(api.Customer,"POST","customer/notifications/read-all",new{});
        api.Checks.Check(await OrderWorkflowSqlScenarios.JsonSnapshot(api.Database,"SELECT * FROM dbo.Order_Views ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES")==viewSnapshot,"SQL notification read-all never changes persisted order views");
        await api.Accept(idNew);
        api.Checks.Check((await api.Counts(api.Administrator,"admin")).Orders==1&&(await api.Counts(api.Customer,"customer")).Orders==0,"SQL customer acceptance creates independent administrator attention");
        await api.Request(api.Administrator,"POST","admin/orders/"+idNew+"/corrections",await api.Correction(idNew,false));
        api.Checks.Check((await api.Counts(api.Customer,"customer")).Orders==1,"SQL later actionable correction reopens attention after an order was viewed");
        var list=await api.Request(api.Customer,"GET","customer/notifications?page=1&pageSize=1");
        api.Checks.Check(list["items"]!.AsArray().Count==1&&list["items"]![0]!["targetPath"]!.ToString()=="/account/orders/"+idNew,"SQL notification pagination retains its authorized order deep link");
        await Tickets(api,idNew);
    }
    private static async Task Tickets(OrderWorkflowApi api,string order)
    {
        var historical=await api.Fixtures.Ticket(order,"Readonly synthetic archive");
        var before=await OrderWorkflowSqlScenarios.JsonSnapshot(api.Database,"SELECT (SELECT COUNT(*) FROM dbo.Tickets) tickets,(SELECT COUNT(*) FROM dbo.Ticket_Messages) messages,(SELECT COUNT(*) FROM dbo.Notifications) notices FOR JSON PATH");
        foreach(var path in new[]{"tickets","contact"})foreach(var payload in new[]{new{consultation=false,orderId=order},new{consultation=true,orderId=""}})
            await api.Request(api.Customer,"POST",path,payload,503,"SUPPORT_SCHEMA_PENDING");
        await api.Request(api.Customer,"POST","tickets/"+historical+"/messages",new{message="No write"},503,"SUPPORT_SCHEMA_PENDING");
        await api.Request(api.Administrator,"POST","admin/tickets/"+historical+"/messages",new{message="No write"},503,"SUPPORT_SCHEMA_PENDING");
        await api.Request(api.Administrator,"PATCH","admin/tickets/"+historical,new{status="closed"},503,"SUPPORT_SCHEMA_PENDING");
        foreach(var scope in new[]{"customer","admin"})
            await api.Request(scope=="customer"?api.Customer:api.Administrator,"POST","realtime/"+scope+"/support/negotiate?negotiateVersion=1",new{},200);
        await api.Request(api.Guest,"POST","tickets",new{},401);await api.Request(api.Customer,"POST","admin/tickets/"+historical+"/messages",new{},401);
        await api.Request(api.Other,"GET","tickets/"+historical,expected:404);
        var archive=await api.Request(api.Customer,"GET","tickets/"+historical);var admin=await api.Request(api.Administrator,"GET","admin/tickets/"+historical);
        api.Checks.Check(archive["readOnly"]!.GetValue<bool>()&&admin["readOnly"]!.GetValue<bool>()&&archive["messages"]!.AsArray().Count==2,"SQL historical ticket messages remain authorized and read-only for both roles");
        api.Checks.Check(await OrderWorkflowSqlScenarios.JsonSnapshot(api.Database,"SELECT (SELECT COUNT(*) FROM dbo.Tickets) tickets,(SELECT COUNT(*) FROM dbo.Ticket_Messages) messages,(SELECT COUNT(*) FROM dbo.Notifications) notices FOR JSON PATH")==before,"SQL009 historical fixture requires the later read-state migration for support writes; negotiation has no SQL side effects");
    }
}
