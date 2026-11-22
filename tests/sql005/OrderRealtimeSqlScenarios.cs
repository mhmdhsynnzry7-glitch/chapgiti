using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderRealtimeSqlScenarios
{
    public static async Task Run(OrderWorkflowApi api)
    {
        foreach(var (client,role) in new[]{(api.Guest,"customer"),(api.Customer,"admin"),(api.Administrator,"customer")})
            await api.Request(client,"POST","realtime/"+role+"/notifications/negotiate?negotiateVersion=1",new{},401);
        await using var customer=await SqlRealtimeSocket.Open(api,api.Customer,api.CustomerCookies,"customer");
        await using var admin=await SqlRealtimeSocket.Open(api,api.Administrator,api.AdministratorCookies,"admin");
        await using var other=await SqlRealtimeSocket.Open(api,api.Other,api.OtherCookies,"customer");
        var id=await api.Create();await SqlRealtimeSocket.Eventually(()=>admin.Events.Count==1);
        api.Checks.Check(customer.Events.IsEmpty&&other.Events.IsEmpty&&(await api.Database.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE entity_id=@p0 AND recipient_kind='admin'",id))!.Num("n")==1,"real SQL commit dispatches a SignalR hint only to the intended administrator after persistence");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/open",new{});
        var quote=await api.Command(id);quote["amountToman"]=200000;quote["scopeText"]="SQL realtime quote";quote["deliveryTerms"]="";quote["exclusionsText"]="";
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/quotes",quote);
        await SqlRealtimeSocket.Eventually(()=>customer.Events.Count==1);
        api.Checks.Check(other.Events.IsEmpty&&admin.Events.Count==1&&(await api.Database.Read("SELECT COUNT(*) n FROM dbo.Order_Quotes WHERE order_id=@p0",id))!.Num("n")==1,"real SQL customer quote notice dispatch is owner-isolated and sees committed quote data");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/quotes",quote);
        await Task.Delay(150);
        api.Checks.Check(customer.Events.Count==1,"idempotent SQL quote replay does not emit a duplicate SignalR event");
        var before=await OrderWorkflowSqlScenarios.Snapshot(api.Database);var version=await api.Version(id);
        await OrderWorkflowSqlScenarios.FailNotification(api,async()=>{
            var rejected=await api.Command(id);rejected["amountToman"]=220000;rejected["scopeText"]="Rollback after notification projection";
            await api.Request(api.Administrator,"POST","admin/orders/"+id+"/quotes",rejected,500);
        },afterProjection:true);
        await Task.Delay(150);
        api.Checks.Check(customer.Events.Count==1&&other.Events.IsEmpty&&await api.Version(id)==version&&await OrderWorkflowSqlScenarios.Snapshot(api.Database)==before,"SQL rollback after notification insert/enqueue publishes no phantom SignalR event and restores business rows");
        foreach(var method in new[]{"SendMessage","JoinTicket","StartPrinting"})
            api.Checks.Check((await customer.Invoke(method))["error"]!=null,"notification hub exposes no business/ticket command: "+method);
        api.Checks.Check(customer.Events.All(e=>e.AsObject().Count==2&&e["kind"]!.ToString()=="notifications"&&e["eventId"]!.ToString().Length==64),"SQL realtime messages contain opaque invalidation IDs only, with no private order/file data");
        await customer.DisposeAsync();
        var counts=await api.Counts(api.Customer,"customer");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,false));
        await using var reconnected=await SqlRealtimeSocket.Open(api,api.Customer,api.CustomerCookies,"customer");
        var resynced=await api.Counts(api.Customer,"customer");var detail=await api.CustomerDetail(id);
        api.Checks.Check(resynced.Notices==counts.Notices+1&&detail["corrections"]!.AsArray().Last()!["state"]!.ToString()=="requested","real SignalR reconnect and HTTP resync recover the SQL correction notice missed while offline");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/view",new{seenThroughSequence=detail["viewSequence"]!.GetValue<long>()});
        await SqlRealtimeSocket.Eventually(()=>reconnected.Events.Count==1);
        using(var newSession=api.Host.Client())
        {
            await api.Request(newSession,"POST","customer/login",new{phone=OrderWorkflowFixtures.Phone,password=OrderWorkflowFixtures.Password});
            api.Checks.Check(await api.Counts(newSession,"customer")==await api.Counts(api.Customer,"customer"),"SQL badges and unread notifications survive websocket replacement and a separate authenticated HTTP session");
        }
        api.Checks.Check(other.Events.IsEmpty,"another customer's SignalR connection receives no correction, quote or view update");
    }
}
