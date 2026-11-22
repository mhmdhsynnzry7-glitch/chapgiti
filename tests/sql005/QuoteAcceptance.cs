using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class QuoteAcceptance
{
    public static async Task Run(IsolatedDatabase db,AcceptanceHost host,AcceptanceChecks checks,HttpClient admin,HttpClient customer,HttpClient other)
    {
        var legacy=Guid.NewGuid().ToString();
        await db.Write("INSERT dbo.Orders(id,code,customer,phone,title,service,spec,estimate,final_price,status,created_at,updated_at,customer_id) SELECT TOP(1) @p0,@p1,customer,phone,N'Pre-006 approved legacy',service,spec,estimate,123456,'confirmed',created_at,updated_at,customer_id FROM dbo.Orders",legacy,Guid.NewGuid().ToString("N")[..20]);
        var before=await db.Read("SELECT COUNT(*) n,SUM(estimate) estimates,SUM(COALESCE(final_price,0)) finals,CHECKSUM_AGG(BINARY_CHECKSUM(id,status,estimate,final_price,spec)) fingerprint FROM dbo.Orders");
        db.Stage("006_order_quotes.sql");await db.Migrate();await db.Migrate();
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n")==6,"006 runs once through the checksum runner and reruns safely");
        var after=await db.Read("SELECT COUNT(*) n,SUM(estimate) estimates,SUM(COALESCE(final_price,0)) finals,CHECKSUM_AGG(BINARY_CHECKSUM(id,status,estimate,final_price,spec)) fingerprint FROM dbo.Orders");
        checks.Check(new[]{"n","estimates","finals","fingerprint"}.All(key=>before!.Num(key)==after!.Num(key))&&
            (await db.Read("SELECT COUNT(*) n FROM dbo.Orders WHERE workflow_version<>1"))!.Num("n")==0,"006 preserves historical orders/prices and marks them legacy");
        var migrationFile=File.ReadAllText("backend/ChapGiti.Infrastructure/Persistence/Migrations/006_order_quotes.sql");
        var checksum=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(migrationFile.Replace("\r\n","\n"))));
        checks.Check((await db.Read("SELECT checksum FROM dbo.Schema_Migrations WHERE name='006_order_quotes.sql'"))!.Str("checksum")==checksum,"006 recorded checksum matches the prepared migration");
        await checks.Request(admin,"POST","admin/orders/"+legacy+"/start-printing",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=1});
        checks.Check((await db.Read("SELECT final_price FROM dbo.Orders WHERE id=@p0",legacy))!.Num("final_price")==123456&&
            (await db.Read("SELECT COUNT(*) n FROM dbo.Order_Quotes WHERE order_id=@p0",legacy))!.Num("n")==0,"historical confirmed order starts with its preserved price without invented customer acceptance");
        var spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        var created=await checks.Request(customer,"POST","orders",new{idempotencyKey=Guid.NewGuid().ToString(),title="SQL proposal request",spec},201);
        var id=created["id"]!.ToString();
        checks.Check((await db.Read("SELECT workflow_version FROM dbo.Orders WHERE id=@p0",id))!.Num("workflow_version")==2,"new SQL inserts default to the customer-approval workflow");
        async Task<JsonNode> Read()=>await checks.Request(admin,"GET","admin/orders/"+id);
        async Task<long> Version()=>(await Read())["order"]!["version"]!.GetValue<long>();
        await checks.Request(admin,"POST","admin/orders/"+id+"/open");
        await checks.Request(admin,"POST","admin/orders/"+id+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=await Version(),amountToman=9876543210L,scopeText="SQL service scope",deliveryTerms="SQL delivery terms",exclusionsText="SQL exclusions"});
        var detail=await Read();var quote=detail["quotes"]!.AsArray().Last()!;
        checks.Check(detail["order"]!["final_price"]==null&&detail["order"]!["status"]!.ToString()=="awaiting_customer_approval","long status and bigint quote amount persist without premature final price");
        object Decision()=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=detail["order"]!["version"]!.GetValue<int>(),quoteId=quote["id"]!.ToString(),quoteVersion=quote["version"]!.GetValue<int>(),reason="Please revise delivery"};
        await checks.Request(other,"POST","customer/orders/"+id+"/quote/accept",Decision(),404);
        await checks.Request(admin,"POST","admin/orders/"+id+"/start-printing",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=await Version()},409);
        await checks.Request(customer,"POST","customer/orders/"+id+"/quote/request-revision",Decision());
        await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(),409);
        await checks.Request(admin,"POST","admin/orders/"+id+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=await Version(),amountToman=7654321000L,scopeText="Revised SQL scope",deliveryTerms="Revised delivery",exclusionsText=""});
        detail=await Read();quote=detail["quotes"]!.AsArray().Last()!;
        await db.Write("CREATE TRIGGER dbo.QuoteAcceptance_RollbackProbe ON dbo.Order_Commands AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE kind='accept') THROW 51001,'Isolated rollback probe',1; END");
        await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",Decision(),500);
        checks.Check((await db.Read("SELECT final_price FROM dbo.Orders WHERE id=@p0",id))!["final_price"]==null&&
            (await db.Read("SELECT state FROM dbo.Order_Quotes WHERE id=@p0",quote["id"]!.ToString()))!.Str("state")=="sent","real SQL failure rolls back quote acceptance and final-price/status changes");
        await db.Write("DROP TRIGGER dbo.QuoteAcceptance_RollbackProbe");
        var accept=Decision();await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",accept);await checks.Request(customer,"POST","customer/orders/"+id+"/quote/accept",accept);
        var activity=await checks.Request(admin,"GET","admin/activity?type=order_status&page=1&pageSize=50");
        checks.Check(activity["items"]!.AsArray().Any(row=>row!["order_id"]?.ToString()==id&&row["status"]?.ToString()=="confirmed"&&row["actor"]?.ToString()==row["customer_name"]?.ToString()),"real SQL activity attributes owner approval without exposing technical actor prefixes");
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Orders o JOIN dbo.Order_Quotes q ON q.order_id=o.id WHERE o.id=@p0 AND o.status='confirmed' AND q.state='accepted' AND o.final_price=q.amount_toman AND q.accepted_by_customer=o.customer_id",id))!.Num("n")==1,"real SQL acceptance atomically aligns owner, quote amount, accepted state and queued order");
        var started=new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=await Version()};
        await checks.Request(admin,"POST","admin/orders/"+id+"/start-printing",started);await checks.Request(admin,"POST","admin/orders/"+id+"/start-printing",started);
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Events WHERE order_id=@p0 AND status='printing'",id))!.Num("n")==1,"real SQL repeated production command creates one event");
        var publicDetail=await checks.Request(customer,"GET","customer/orders/"+id);
        checks.Check(!publicDetail.ToJsonString().Contains("created_by_admin")&&!publicDetail.ToJsonString().Contains("admin_notes"),"SQL-backed customer quote contract contains no private administrative fields");
        var grouped=await checks.Request(customer,"GET","customer/orders?page=1&group=production");
        checks.Check(grouped["items"]!.AsArray().Any(o=>o!["id"]!.ToString()==id),"real SQL customer pagination/grouping reads proposal workflow records");
    }
}
