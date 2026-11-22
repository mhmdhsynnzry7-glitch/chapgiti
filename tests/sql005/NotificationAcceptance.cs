using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Application.Notifications;
using ChapGiti.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;

namespace ChapGiti.SqlAcceptanceTests;

internal static class NotificationAcceptance
{
    public static async Task Run(IsolatedDatabase db, AcceptanceHost host, AcceptanceChecks checks, HttpClient admin, HttpClient customer, HttpClient other)
    {
        var unavailable = await checks.Request(customer,"GET","customer/notifications/unread-count",expected:503);
        checks.Check(unavailable["code"]!.ToString()=="NOTIFICATIONS_UNAVAILABLE","pre-007 storage is explicitly unavailable without blocking existing business compatibility");
        var before=await db.Read("SELECT COUNT(*) n,CHECKSUM_AGG(BINARY_CHECKSUM(id,status,estimate,final_price,spec)) fingerprint FROM dbo.Orders");
        db.Stage("007_in_app_notifications.sql");await db.Migrate();await db.Migrate();
        var source=File.ReadAllText("backend/ChapGiti.Infrastructure/Persistence/Migrations/007_in_app_notifications.sql");
        var checksum=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Replace("\r\n","\n"))));
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n")==7&&
            (await db.Read("SELECT checksum FROM dbo.Schema_Migrations WHERE name='007_in_app_notifications.sql'"))!.Str("checksum")==checksum,"007 registers its reviewed checksum and reruns without duplicate schema changes");
        var after=await db.Read("SELECT COUNT(*) n,CHECKSUM_AGG(BINARY_CHECKSUM(id,status,estimate,final_price,spec)) fingerprint FROM dbo.Orders");
        checks.Check(before!.Num("n")==after!.Num("n")&&before!.Num("fingerprint")==after!.Num("fingerprint")&&
            (await db.Read("SELECT COUNT(*) n FROM dbo.Notifications"))!.Num("n")==0,"007 preserves historical requests and never fabricates historical notifications");
        checks.Check((await db.Read("SELECT COUNT(*) n FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.Notifications') AND is_disabled=0 AND is_not_trusted=0"))!.Num("n")==2&&
            (await db.Read("SELECT COUNT(*) n FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Notifications') AND name LIKE 'IX_Notifications_%'"))!.Num("n")==4,"notification recipient FKs and paging/unread indexes exist and are trusted");
        using var guest=host.Client();await checks.Request(guest,"GET","customer/notifications",expected:401);await checks.Request(customer,"GET","admin/notifications",expected:401);await checks.Request(admin,"GET","customer/notifications",expected:401);
        var spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        var key=Guid.NewGuid().ToString();var creation=new{idempotencyKey=key,title="SQL notification request",spec};
        var result=await checks.Request(customer,"POST","orders",creation,201);await checks.Request(customer,"POST","orders",creation);
        var id=result["id"]!.ToString();
        async Task<long> Count(string type)=>(await db.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE entity_id=@p0 AND event_type=@p1",id,type))!.Num("n");
        async Task<JsonNode> Detail()=>await checks.Request(admin,"GET","admin/orders/"+id);
        async Task<long> Version()=>(await Detail())["order"]!["version"]!.GetValue<long>();
        checks.Check(await Count("order.created")==1,"real SQL new-request retry persists exactly one admin notification");
        await checks.Request(admin,"POST","admin/orders/"+id+"/open");await Detail();
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE entity_id=@p0",id))!.Num("n")==1,"opening and GET do not create additional SQL notifications");
        var send=new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=await Version(),amountToman=450000,scopeText="SQL notification commercial scope",deliveryTerms="",exclusionsText=""};
        await checks.Request(admin,"POST","admin/orders/"+id+"/quotes",send);await checks.Request(admin,"POST","admin/orders/"+id+"/quotes",send);
        checks.Check(await Count("quote.issued")==1,"SQL proposal projection is deduplicated by actual recipient and event");
        var detail=await Detail();var quote=detail["quotes"]!.AsArray().Last()!;
        var decision=new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=detail["order"]!["version"]!.GetValue<long>(),quoteId=quote["id"]!.ToString(),quoteVersion=quote["version"]!.GetValue<long>()};
        await checks.Request(other,"POST","customer/orders/"+id+"/quote/accept",decision,404);
        await db.Write("CREATE TRIGGER dbo.NotificationAcceptance_RollbackProbe ON dbo.Notifications AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE event_type='quote.accepted') THROW 51002,'Isolated notification rollback probe',1; END");
        await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",decision,500);
        checks.Check(await Count("quote.accepted")==0&&(await db.Read("SELECT status FROM dbo.Orders WHERE id=@p0",id))!.Str("status")=="awaiting_customer_approval"&&
            (await db.Read("SELECT state FROM dbo.Order_Quotes WHERE id=@p0",quote["id"]!.ToString()))!.Str("state")=="sent"&&
            (await db.Read("SELECT COUNT(*) n FROM dbo.Order_Commands WHERE id=@p0",decision.idempotencyKey))!.Num("n")==0,"real SQL notification failure rolls back its row, owner acceptance, order status and command receipt together");
        await db.Write("DROP TRIGGER dbo.NotificationAcceptance_RollbackProbe");
        await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",decision);await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",decision);
        checks.Check(await Count("quote.accepted")==1,"successful retry after rollback commits one approval notification");
        var start=new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=await Version()};
        await checks.Request(admin,"POST","admin/orders/"+id+"/start-printing",start);await checks.Request(admin,"POST","admin/orders/"+id+"/start-printing",start);
        foreach(var status in new[]{"binding","ready","completed"})await checks.Request(admin,"PATCH","admin/orders/"+id,new{idempotencyKey=Guid.NewGuid().ToString(),version=await Version(),status});
        foreach(var status in new[]{"printing","binding","ready","completed"})checks.Check(await Count("order."+status)==1,"SQL persisted one production notification for "+status);
        var page=await checks.Request(customer,"GET","customer/notifications?pageSize=50");var own=page["items"]!.AsArray().First(row=>row!["entityId"]!.ToString()==id)!;
        var notice=own["id"]!.ToString();
        await checks.Request(other,"POST","customer/notifications/"+notice+"/read",new{},404);
        await checks.Request(admin,"POST","admin/notifications/"+notice+"/read",new{},404);
        await checks.Request(customer,"POST","customer/notifications/"+notice+"/read");var readTime=(await db.Read("SELECT read_at FROM dbo.Notifications WHERE id=@p0",notice))!.Str("read_at");
        await checks.Request(customer,"POST","customer/notifications/"+notice+"/read");
        checks.Check(readTime!=""&&readTime==(await db.Read("SELECT read_at FROM dbo.Notifications WHERE id=@p0",notice))!.Str("read_at"),"SQL read-one keeps its first read timestamp and rejects cross-recipient access");
        await checks.Request(customer,"POST","customer/notifications/read-all");
        var unread=await checks.Request(customer,"GET","customer/notifications/unread-count");
        var adminUnread=await checks.Request(admin,"GET","admin/notifications/unread-count");
        checks.Check(unread["unreadCount"]!.GetValue<long>()==0&&adminUnread["unreadCount"]!.GetValue<long>()>0,"SQL mark-all-read leaves independent administrator notifications unread");
        await checks.Request(customer,"POST","customer/logout");await checks.Request(customer,"POST","customer/login",new{phone="09100000001",password="Sql.Acceptance@2026"});
        checks.Check((await checks.Request(customer,"GET","customer/notifications"))["items"]!.AsArray().All(row=>row!["readAt"]!=null),"SQL notification read state survives logout, login and new request scopes");
        foreach(var kind in new[]{"general","order","consultation"})
        {
            var ticketKey=Guid.NewGuid().ToString();var body=new{idempotencyKey=ticketKey,subject="SQL notification support",message="Synthetic private support request",orderId=kind=="order"?id:"",consultation=kind=="consultation"};
            var ticket=(await checks.Request(customer,"POST","tickets",body,201))["id"]!.ToString();await checks.Request(customer,"POST","tickets",body);
            var reply=new{idempotencyKey=Guid.NewGuid().ToString(),message="Synthetic private administrator reply"};
            await checks.Request(admin,"POST","admin/tickets/"+ticket+"/messages",reply,201);await checks.Request(admin,"POST","admin/tickets/"+ticket+"/messages",reply);
            await checks.Request(customer,"POST","tickets/"+ticket+"/messages",new{idempotencyKey=Guid.NewGuid().ToString(),message="Synthetic follow up"},201);
            checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE entity_id=@p0",ticket))!.Num("n")==3,"SQL "+kind+" support persists intake and opposite-party reply notifications without retry duplicates");
            checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE entity_id=@p0 AND recipient_kind='customer' AND target_path=@p1",ticket,"/tickets/"+ticket))!.Num("n")==1,"SQL support deep link opens the correct owner's conversation");
        }
        var first=await checks.Request(admin,"GET","admin/notifications?page=1&pageSize=2");var second=await checks.Request(admin,"GET","admin/notifications?page=2&pageSize=2");
        checks.Check(!first["items"]!.AsArray().Select(row=>row!["id"]!.ToString()).Intersect(second["items"]!.AsArray().Select(row=>row!["id"]!.ToString())).Any(),"SQL sequence-based notification pagination remains deterministic at equal timestamps");
        await using(var store=db.Store())
        {
            await db.Guard(store); var repository=new NotificationRepository(store);
            var existing=(await db.Read("SELECT TOP(1) admin_email,dedupe_key,event_type,entity_id,created_at,title,body,target_path FROM dbo.Notifications WHERE recipient_kind='admin' AND entity_kind='order'"))!;
            var recipient=new NotificationRecipient("admin",existing.Str("admin_email"));
            var duplicate=new StoredNotification(recipient,new(Guid.NewGuid().ToString(),existing.Str("event_type"),"order",existing.Str("entity_id"),existing.Str("title"),existing.Str("body"),existing.Str("target_path"),existing.Str("created_at"),null),existing.Str("dedupe_key"));
            // Select an order row specifically; no arbitrary target is reconstructed from user input.
            var total=await repository.CountNotifications(recipient);
            await store.Transaction(async()=>{await repository.InsertNotification(duplicate);return true;});
            checks.Check(await repository.CountNotifications(recipient)==total,"real SQL repository independently deduplicates reprojected business events");
        }
        foreach(var invalidSql in new[]{
            "UPDATE TOP(1) dbo.Notifications SET recipient_kind='customer',customer_id=NULL,admin_email=NULL",
            "UPDATE TOP(1) dbo.Notifications SET target_path=N'https://external.invalid/'",
            "UPDATE TOP(1) dbo.Notifications SET read_at=DATEADD(day,-1,created_at)",
            "UPDATE TOP(1) dbo.Notifications SET customer_id='00000000-0000-0000-0000-000000000000',admin_email=NULL,recipient_kind='customer',target_path=N'/account/orders/'+entity_id WHERE entity_kind='order'"})
        {
            try { await db.Write(invalidSql); checks.Check(false,"notification SQL constraints reject invalid state"); }
            catch(SqlException error) when(error.Number==547) { checks.Check(true,"SQL notification recipient/target/read-time constraint rejects invalid state"); }
        }
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE body LIKE N'%private%' OR body LIKE N'%450000%'"))!.Num("n")==0,"persisted notification bodies exclude private support content and commercial amounts");
        await checks.Request(admin,"PUT","admin/profile/email",new{email="changed-notification-admin@example.test"});
        checks.Check((await checks.Request(admin,"GET","admin/notifications"))["total"]!.GetValue<int>()>0,"editable admin profile email does not orphan SQL notification ownership");
        var fresh=(await checks.Request(customer,"POST","orders",new{idempotencyKey=Guid.NewGuid().ToString(),title="SQL stable admin identity",spec},201))["id"]!.ToString();
        await checks.Request(admin,"POST","admin/orders/"+fresh+"/open");
        var freshDetail=await checks.Request(admin,"GET","admin/orders/"+fresh);
        await checks.Request(admin,"POST","admin/orders/"+fresh+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=freshDetail["order"]!["version"]!.GetValue<long>(),amountToman=120000,scopeText="Stable identity quote",deliveryTerms="",exclusionsText=""});
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Order_Quotes WHERE order_id=@p0 AND created_by_admin='admin@sql-acceptance.invalid'",fresh))!.Num("n")==1,"real SQL quote FK uses the immutable administrator identity after email changes");
        await checks.Request(admin,"PUT","admin/profile/email",new{email="admin@sql-acceptance.invalid"});
    }
}
