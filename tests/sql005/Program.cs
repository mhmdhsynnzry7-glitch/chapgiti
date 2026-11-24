using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Common;
using ChapGiti.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using ChapGiti.SqlAcceptanceTests;

if(args.Length==1&&args[0]=="--authorize-disposable-ef-scaffold")return await ScaffoldEfSchema.Run();
if(args.Length==1&&args[0]=="--authorize-disposable-ef-contract")return await ScaffoldEfSchema.Run(metadataOnly:true);
if(args.Length==1&&args[0]=="--authorize-disposable-ef-modernization")return await EfModernizationAcceptance.Run();
if(args.Length==1&&args[0]=="--authorize-disposable-ef-activation")return await EfActivationAcceptance.Run();
if(args.Length==1&&args[0]=="--authorize-disposable-express2025-activation")return await EfActivationAcceptance.Run(express2025:true);
if(args.Length==1&&args[0]=="--authorize-disposable-express2025-catalog")return await EfModernizationAcceptance.Run(express2025:true);

if(args.Length==1&&args[0]=="--authorize-disposable-localdb-008-009") return await OrderWorkflowAcceptance.Run();

if(args.Length!=1||args[0]!="--authorize-disposable-localdb-005") {
    Console.WriteLine("No SQL operations performed. Explicit --authorize-disposable-localdb-005 is required.");return 2;
}
if(!File.Exists("AGENTS.md")||!Directory.Exists("backend/ChapGiti.Infrastructure/Persistence/Migrations")) {
    Console.WriteLine("Run from the CHAP GITI repository root.");return 2;
}
var checks=new AcceptanceChecks();var database=new IsolatedDatabase();string? failure=null;
var migrations=Directory.GetFiles("backend/ChapGiti.Infrastructure/Persistence/Migrations","*.sql").Where(f=>string.CompareOrdinal(Path.GetFileName(f),"006")<0).Order().ToArray();
var originalHashes=migrations.ToDictionary(f=>Path.GetFileName(f),f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
try {
    await database.Create();
    foreach(var file in migrations.Take(3))database.Stage(Path.GetFileName(file));
    await database.Migrate();
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n")==3,"existing runner applies unchanged 001–003 to the empty isolated catalog");
    var owner=Guid.NewGuid().ToString();var other=Guid.NewGuid().ToString();var order=Guid.NewGuid().ToString();var otherOrder=Guid.NewGuid().ToString();var oldTicket=Guid.NewGuid().ToString();
    const string password="Sql.Acceptance@2026",phone="09100000001",otherPhone="09100000002",email="admin@sql-acceptance.invalid";
    var hash=new PasswordHasher().Hash(password);var now=Input.Now();
    foreach(var customer in new[]{(owner,phone),(other,otherPhone)}) {
        await database.Write("INSERT dbo.Customer_Registrations(id,phone,first_name,last_name,phone_verified,created_at,updated_at) VALUES(@p0,@p1,N'Synthetic',N'Customer',1,@p2,@p2)",customer.Item1,customer.Item2,now);
        await database.Write("INSERT dbo.Customer_Accounts VALUES(@p0,@p1,@p2,@p3)",customer.Item2,customer.Item1,hash,now);
    }
    await database.Write("INSERT dbo.Admin_Users VALUES(@p0,@p1,@p2)",email,hash,now);
    await database.Write("INSERT dbo.Admin_Usernames VALUES('admin',@p0)",email);
    foreach(var record in new[]{(order,owner,phone),(otherOrder,other,otherPhone)})
        await database.Write("INSERT dbo.Orders(id,code,customer,phone,title,service,spec,estimate,admin_notes,created_at,updated_at,customer_id) VALUES(@p0,@p1,N'Synthetic Customer',@p2,N'Acceptance book','book-printing',@p3,440000,N'legacy-private-note',@p4,@p4,@p5)",record.Item1,record.Item1.Replace("-","")[..20].ToUpperInvariant(),record.Item3,"{\"pages\":120,\"quantity\":1,\"details\":{\"printOnly\":false}}",now,record.Item2);
    await database.Write("INSERT dbo.Tickets VALUES(@p0,@p1,N'Historical general ticket','open',@p2,@p2)",oldTicket,owner,now);
    await database.Write("INSERT dbo.Ticket_Messages VALUES(@p0,@p0,'customer',N'Historical general message',@p1)",oldTicket,now);
    await database.Write("INSERT dbo.Settings VALUES(N'acceptance-preserve',N'unchanged')");

    // Prove that 004 is not a direct dependency, then roll back all 005 DDL.
    var sql005=File.ReadAllText(migrations.Single(f=>Path.GetFileName(f).StartsWith("005_")));
    await using(var db=database.Store()) {
        await database.Guard(db);
        try {await db.Transaction<bool>(async()=>{
            await db.Execute(sql005);
            checks.Check((await db.One("SELECT CASE WHEN COL_LENGTH('dbo.Admin_Users','login_email') IS NULL AND COL_LENGTH('dbo.Tickets','order_id')=36 AND OBJECT_ID('dbo.Order_Notes') IS NOT NULL THEN 1 ELSE 0 END n"))!.Num("n")==1,"005 executes on 001–003 without requiring 004");
            throw new RollbackProbe();
        });}catch(RollbackProbe){}
    }
    checks.Check((await database.Read("SELECT CASE WHEN COL_LENGTH('dbo.Tickets','order_id') IS NULL AND OBJECT_ID('dbo.Order_Notes') IS NULL THEN 1 ELSE 0 END n"))!.Num("n")==1,"transaction rollback removes the dependency probe schema");

    await using var host=await AcceptanceHost.Start(database);
    using var customerClient=host.Client();using var otherClient=host.Client();using var adminClient=host.Client();using var anonymous=host.Client();
    await checks.Request(customerClient,"POST","customer/login",new{phone,password});
    await checks.Request(otherClient,"POST","customer/login",new{phone=otherPhone,password});
    await checks.Request(adminClient,"POST","auth/login",new{username="admin",password});
    await checks.Request(customerClient,"POST","tickets",new{idempotencyKey=Guid.NewGuid().ToString(),orderId=order,subject="Not migrated",message="Related request before 005"},503);
    await checks.Request(adminClient,"POST","admin/orders/"+order+"/notes",new{idempotencyKey=Guid.NewGuid().ToString(),note="not yet available"},503);

    foreach(var file in migrations.Skip(3))database.Stage(Path.GetFileName(file));
    await database.Migrate();
    foreach(var file in migrations) {
        var expected=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n","\n"))));
        checks.Check((await database.Read("SELECT checksum FROM dbo.Schema_Migrations WHERE name=@p0",Path.GetFileName(file)))!.Str("checksum")==expected,"registered checksum: "+Path.GetFileName(file));
    }
    var migrationBefore=(await database.Read("SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(name,checksum,CONVERT(varchar(30),applied_at,126))),N'|') WITHIN GROUP(ORDER BY name) snapshot FROM dbo.Schema_Migrations"))!.Str("snapshot");
    await database.Migrate();
    checks.Check((await database.Read("SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(name,checksum,CONVERT(varchar(30),applied_at,126))),N'|') WITHIN GROUP(ORDER BY name) snapshot FROM dbo.Schema_Migrations"))!.Str("snapshot")==migrationBefore,"rerunning runner preserves migration entries/timestamps without duplicate DDL");
    var staged005=Path.Combine(database.DirectoryPath,"Data",Path.GetFileName(migrations[^1]));
    File.AppendAllText(staged005,"\n-- isolated checksum rejection probe\n");
    try {await database.Migrate();checks.Check(false,"changed migration checksum must be rejected");}
    catch(InvalidOperationException error) when(error.Message.StartsWith("Applied SQL Server migration was modified:")) {checks.Check(true,"runner rejects a changed staged checksum before schema modification");}
    finally {database.Stage(Path.GetFileName(migrations[^1]));}
    checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.Tickets'),OBJECT_ID('dbo.Order_Notes')) AND referenced_object_id=OBJECT_ID('dbo.Orders') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0"))!.Num("n")==2,"both new order foreign keys are enabled, trusted and non-cascading");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.indexes WHERE name IN('IX_Tickets_Order','IX_OrderNotes_Order') AND is_disabled=0"))!.Num("n")==2,"order relationship and note-history indexes exist");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Order_Notes') AND ((name='note' AND TYPE_NAME(user_type_id)='nvarchar' AND max_length=-1 AND is_nullable=0) OR (name='actor' AND max_length=508 AND is_nullable=0) OR (name='created_at' AND TYPE_NAME(user_type_id)='datetime2' AND scale=3 AND is_nullable=0))"))!.Num("n")==3,"note text/actor/time SQL types and nullability match repository contract");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.check_constraints WHERE name='CK_OrderNotes_NoteLength' AND is_disabled=0 AND is_not_trusted=0"))!.Num("n")==1,"trusted SQL CHECK enforces the 5000 UTF-16-unit note limit");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Tickets WHERE id=@p0 AND order_id IS NULL AND subject=N'Historical general ticket'",oldTicket))!.Num("n")==1,"pre-005 ticket becomes nullable-order general ticket with history unchanged");

    var linked=Guid.NewGuid().ToString();var ticketBody=new{idempotencyKey=linked,orderId=order,subject="Linked acceptance",message="A real SQL persisted support request."};
    await checks.Request(customerClient,"POST","tickets",ticketBody,201);
    await checks.Request(customerClient,"POST","tickets",ticketBody);
    await checks.Request(customerClient,"POST","tickets",new{idempotencyKey=Guid.NewGuid().ToString(),orderId=otherOrder,subject="Foreign order",message="This must not be persisted."},404);
    await checks.Request(otherClient,"GET","tickets/"+linked,expected:404);
    await checks.Request(otherClient,"POST","tickets/"+linked+"/messages",new{idempotencyKey=Guid.NewGuid().ToString(),message="forbidden"},404);
    await checks.Request(anonymous,"GET","admin/tickets/"+linked,expected:401);
    var adminTicket=await checks.Request(adminClient,"GET","admin/tickets/"+linked);
    checks.Check(adminTicket["ticket"]!["order_id"]!.ToString()==order&&adminTicket["ticket"]!["order_title"]!.ToString()=="Acceptance book","administrator related-order projection uses actual SQL relationship");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Ticket_Messages WHERE ticket_id=@p0",linked))!.Num("n")==1,"ticket idempotency persists exactly one initial message");
    await checks.Request(customerClient,"POST","tickets",new{idempotencyKey=Guid.NewGuid().ToString(),subject="General acceptance",message="General request after migration."},201);
    await checks.Request(customerClient,"POST","tickets/"+oldTicket+"/messages",new{idempotencyKey=Guid.NewGuid().ToString(),message="Historical ticket still works"},201);
    await checks.Request(adminClient,"POST","admin/tickets/"+linked+"/messages",new{idempotencyKey=Guid.NewGuid().ToString(),message="Support SQL response"},201);
    await checks.Request(adminClient,"PATCH","admin/tickets/"+linked,new{status="awaiting_customer"});
    var waiting=await checks.Request(adminClient,"GET","admin/tickets?page=1&pageSize=1&status=awaiting_customer&q=Linked");
    checks.Check(waiting["total"]!.GetValue<int>()==1&&waiting["items"]![0]!["last_message"]!.ToString()=="Support SQL response","SQL status/search/pagination and last-message preview agree");
    await checks.Request(customerClient,"POST","tickets/"+linked+"/messages",new{idempotencyKey=Guid.NewGuid().ToString(),message="Customer SQL response"},201);
    await checks.Request(adminClient,"PATCH","admin/tickets/"+linked,new{status="closed"});
    await checks.Request(customerClient,"POST","tickets/"+linked+"/messages",new{idempotencyKey=Guid.NewGuid().ToString(),message="closed reply"},409);
    await checks.Request(adminClient,"PATCH","admin/tickets/"+linked,new{status="open"});
    var pageOne=await checks.Request(customerClient,"GET","tickets?page=1&pageSize=1");var pageTwo=await checks.Request(customerClient,"GET","tickets?page=2&pageSize=1");
    checks.Check(pageOne["total"]!.GetValue<int>()==3&&pageOne["items"]![0]!["id"]!.ToString()!=pageTwo["items"]![0]!["id"]!.ToString(),"SQL owned pagination reaches distinct conversations, including historical null-order tickets");
    await checks.Request(customerClient,"POST","admin/orders/"+order+"/notes",new{idempotencyKey=Guid.NewGuid().ToString(),note="forbidden"},401);
    var noteId=Guid.NewGuid().ToString();var noteBody=new{idempotencyKey=noteId,note="یادداشت خصوصی آزمایشی SQL"};
    await checks.Request(adminClient,"POST","admin/orders/"+order+"/notes",noteBody);
    await checks.Request(adminClient,"POST","admin/orders/"+order+"/notes",noteBody);
    await checks.Request(adminClient,"POST","admin/orders/"+order+"/notes",new{idempotencyKey=noteId,note="different payload"},409);
    await checks.Request(adminClient,"POST","admin/orders/"+order+"/notes",new{idempotencyKey=Guid.NewGuid().ToString(),note=new string('ن',5000)});
    await checks.Request(adminClient,"POST","admin/orders/"+order+"/notes",new{idempotencyKey=Guid.NewGuid().ToString(),note=new string('ن',5001)},400);
    var orderDetail=await checks.Request(adminClient,"GET","admin/orders/"+order);
    checks.Check(orderDetail["notesAvailable"]!.GetValue<bool>()&&orderDetail["internalNotes"]!.AsArray().Count==2&&orderDetail["order"]!["admin_notes"]!.ToString()=="legacy-private-note","real SQL append/history preserves legacy private note and supports 5000 Unicode characters");
    checks.Check(orderDetail["internalNotes"]!.AsArray().All(n=>n!["actor"]!.ToString()==email&&n["created_at"]!=null),"note author and timestamp survive SQL persistence");
    var customerOrder=await checks.Request(customerClient,"GET","customer/orders/"+order);
    var tracked=await checks.Request(anonymous,"POST","track",new{code=orderDetail["order"]!["code"]!.ToString(),phone});
    checks.Check(new[]{customerOrder,tracked}.All(r=>!r.ToJsonString().Contains("legacy-private-note")&&!r.ToJsonString().Contains("internalNotes")&&!r.ToJsonString().Contains("یادداشت خصوصی")),"private note sources never leak through owner or public tracking APIs");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Order_Notes WHERE order_id=@p0",order))!.Num("n")==2,"note replay/invalid input never adds SQL rows");
    foreach(var tooLong in new[]{new string('ن',5001),new string('ن',5000)+" "}) {
        try {await database.Write("INSERT dbo.Order_Notes VALUES(@p0,@p1,@p2,N'test',SYSUTCDATETIME())",Guid.NewGuid().ToString(),order,tooLong);checks.Check(false,"oversized raw SQL note must fail");}
        catch(SqlException error) when(error.Number==547){checks.Check(true,"SQL length CHECK rejects oversized Unicode note, including trailing whitespace");}
    }
    foreach(var sql in new[]{"INSERT dbo.Order_Notes VALUES(@p0,@p1,N'x',N'test',SYSUTCDATETIME())","UPDATE dbo.Tickets SET order_id=@p1 WHERE id=@p0"}) {
        try {await database.Write(sql,sql.StartsWith("INSERT")?Guid.NewGuid().ToString():linked,Guid.NewGuid().ToString());checks.Check(false,"foreign-key violation must fail");}
        catch(SqlException error) when(error.Number==547){checks.Check(true,"SQL foreign key rejects nonexistent order without an orphan");}
    }
    await using(var db=database.Store()) {
        await database.Guard(db);
        try{await db.Transaction<bool>(async()=>{await db.Execute("INSERT dbo.Order_Notes VALUES(@p0,@p1,N'rollback probe',N'test',SYSUTCDATETIME())",Guid.NewGuid().ToString(),order);throw new RollbackProbe();});}catch(RollbackProbe){}
    }
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Order_Notes WHERE note=N'rollback probe'"))!.Num("n")==0,"real SQL transaction rolls back an interrupted note append");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Settings WHERE [key]=N'acceptance-preserve' AND [value]=N'unchanged'"))!.Num("n")==1,"unrelated settings survive migrations and acceptance flows");
    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Admin_Users WHERE email=@p0 AND password_hash=@p1",email,hash))!.Num("n")==1,"migration chain and note writes preserve existing administrator credentials");
    checks.Check((await database.Read("SELECT (SELECT COUNT(*) FROM dbo.Tickets t LEFT JOIN dbo.Orders o ON o.id=t.order_id WHERE t.order_id IS NOT NULL AND o.id IS NULL)+(SELECT COUNT(*) FROM dbo.Order_Notes n LEFT JOIN dbo.Orders o ON o.id=n.order_id WHERE o.id IS NULL) n"))!.Num("n")==0,"no orphaned order links or notes remain");
    await QuoteAcceptance.Run(database,host,checks,adminClient,customerClient,otherClient);
    await NotificationAcceptance.Run(database,host,checks,adminClient,customerClient,otherClient);
    // Independent acceptance phases share this disposable catalog. Expire only its synthetic
    // intake counters; do not weaken production limits or touch any operational records.
    await database.Write("UPDATE dbo.Rate_Limits SET expires=0 WHERE [key] LIKE 'ticket-create:%'");
    await AuditWorkflows.Run(database,host,checks,adminClient);
    checks.Check(migrations.All(f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))==originalHashes[Path.GetFileName(f)]),"all original migration files 001–005 remain byte-for-byte unchanged");
    if(Environment.GetEnvironmentVariable("CHAP_SQL_BROWSER_AUDIT")=="true") {
        await BrowserAudit.Run(database,host,checks);
        host.AllowIsolatedFrontend(host.Origin);
    }
    await CmsAcceptance.Run(database,host,checks,adminClient);
} catch(Exception error) {
    failure=error is SqlException sql?"SQL "+sql.Number+": "+sql.Message:error.Message;
    Console.WriteLine("FAIL "+failure);
} finally {
    try {await database.DisposeAsync();}catch(Exception error){failure=(failure??"")+" Cleanup: "+error.Message;Console.WriteLine("FAIL disposable cleanup unconfirmed");}
    File.WriteAllText(Path.Combine(database.DirectoryPath,"result.json"),JsonSerializer.Serialize(new {checks=checks.Passed,failed=failure==null?0:1,failure,database=database.Name,instance=database.Instance,removed=database.Removed,operationalWrites=false,realSms=false,published=false},new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine(checks.Passed.Count+" SQL acceptance checks passed; failed="+(failure==null?0:1));
return failure==null?0:1;

internal sealed class RollbackProbe : Exception;

