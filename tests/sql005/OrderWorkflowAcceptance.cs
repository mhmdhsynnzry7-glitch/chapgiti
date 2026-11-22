using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChapGiti.Application.Common;
using Microsoft.Data.SqlClient;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderWorkflowAcceptance
{
    internal static readonly string ArtifactRoot=Path.GetFullPath(".qa/order-workflow-sql-2026-11-17");
    private static readonly string MigrationRoot="backend/ChapGiti.Infrastructure/Persistence/Migrations";
    public static async Task<int> Run()
    {
        if(!File.Exists("AGENTS.md")||!Directory.Exists(MigrationRoot)) {Console.WriteLine("Run from the repository root; no SQL was accessed.");return 2;}
        Directory.CreateDirectory(ArtifactRoot);
        var files=Directory.GetFiles(MigrationRoot,"*.sql").OrderBy(Path.GetFileName,StringComparer.Ordinal).ToArray();
        var inventory=files.Select(file=>new {name=Path.GetFileName(file),checksum=Checksum(file)}).ToArray();
        var checks=new AcceptanceChecks();var phases=new List<object>();var failures=new List<string>();var blocked=new List<string>();
        checks.Check(inventory.Select((f,i)=>f.name.StartsWith((i+1).ToString("D3")+"_",StringComparison.Ordinal)).All(x=>x)&&inventory.Length==9,"actual ordered migration inventory contains exactly 001–009");
        foreach(var upgrade in new[]{true,false})
        {
            var database=new IsolatedDatabase();var label=upgrade?"upgrade-001-007-to-009":"fresh-001-009";
            var fixtures=new OrderWorkflowFixtures(database);var privateRemoved=false;
            Dictionary<string,object?>? engine=null;string? failure=null;var before=checks.Passed.Count;
            try
            {
                await database.Create();
                engine=await database.Read("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) productVersion,CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) edition,CONVERT(int,SERVERPROPERTY('IsLocalDB')) isLocalDb,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) serverName,DB_NAME() databaseName,@@VERSION version");
                checks.Check(engine!.Num("isLocalDb")==1&&engine.Str("databaseName")==database.Name&&engine.Str("serverName")==database.Server,"actual SQL LocalDB server and disposable catalog identity match before migration writes");
                Console.WriteLine("SQL_ENGINE="+engine.Str("productVersion")+" EDITION="+engine.Str("edition"));
                foreach(var file in files.Take(upgrade?7:9))database.Stage(Path.GetFileName(file));
                await Task.WhenAll(database.Migrate(),database.Migrate());
                checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n")== (upgrade?7:9),label+" initial runner registration");
                if(upgrade)
                {
                    await fixtures.SeedHistorical();var historicalBefore=await fixtures.Snapshot();
                    database.Stage(inventory[7].name);await Task.WhenAll(database.Migrate(),database.Migrate());
                    database.Stage(inventory[8].name);await database.Migrate();
                    var historicalAfter=await fixtures.Snapshot();
                    foreach(var (table,hash) in historicalBefore)checks.Check(historicalAfter[table]==hash,"upgrade preserves historical "+table+" values byte-for-byte");
                    await fixtures.VerifyPrivateHistory(checks);
                    var legacy=await database.Read("SELECT * FROM dbo.Order_Corrections WHERE order_id=@p0",fixtures.Pending);
                    checks.Check(legacy!=null&&Convert.ToBoolean(legacy["is_legacy"])&&legacy["requested_at"]==null&&legacy["requested_by_admin"]==null&&legacy.Str("base_file_id")==fixtures.OriginalFile,"pending historical request is backfilled without fabricated authors/times or a newly uploaded PDF");
                    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Files WHERE file_version IS NULL AND correction_id IS NULL"))!.Num("n")==2,"historical attachment versions remain explicitly unknown");
                    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Order_Views"))!.Num("n")==0,"activation creates no invented historical order views");
                }
                else await fixtures.SeedIdentities();
                foreach(var file in inventory)
                    checks.Check((await database.Read("SELECT checksum FROM dbo.Schema_Migrations WHERE name=@p0",file.name))!.Str("checksum")==file.checksum,label+" checksum "+file.name);
                var history=await History(database);await database.Migrate();
                checks.Check(await History(database)==history,label+" rerun preserves the exact migration history and applied timestamps");
                checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.Order_Corrections'),OBJECT_ID('dbo.Order_Views'),OBJECT_ID('dbo.Files')) AND (is_disabled=1 OR is_not_trusted=1)"))!.Num("n")==0,"new correction/view/file foreign keys are enabled and trusted");
                checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.indexes WHERE name IN('UX_OrderCorrections_Active','UX_Files_OrderVersion','UX_OrderViews_Customer','UX_OrderViews_Admin','IX_Notifications_CustomerOrderAttention','IX_Notifications_AdminOrderAttention','IX_OrderQuotes_AcceptedHistory') AND is_disabled=0"))!.Num("n")==7,"all correction, version, attention and accepted-history indexes exist");
                await OrderWorkflowSchemaChecks.Run(database,checks);
                await MigrationRunnerSqlChecks.Run(database,checks);
                await using(var db=database.Store())
                {
                    await database.Guard(db);
                    await ChapGiti.Infrastructure.Persistence.Ef.LegacyEfMigrations.Apply(db);
                }
                await using var host=await AcceptanceHost.Start(database);
                using var api=new OrderWorkflowApi(database,fixtures,host,checks);await api.Login();
                if(upgrade)
                {
                    checks.Check(await api.Counts(api.Customer,"customer")== (1,0),"historical unread notification remains separate from activation-baselined order attention");
                    var completed=await api.Detail(fixtures.Completed);checks.Check(completed["order"]!["status"]!.ToString()=="completed"&&completed["order"]!["final_price"]!.GetValue<long>()==123456,"historical completed production is never reset by migration or GET");
                    await api.Request(api.Administrator,"PATCH","admin/orders/"+fixtures.Pending,new{version=await api.Version(fixtures.Pending),status="printing",finalPrice=100},409,"ORDER_CORRECTION_REQUIRED");
                    await api.Request(api.Customer,"POST","customer/orders/"+fixtures.Pending+"/corrections/submit",await api.Submission(fixtures.Pending,fixtures.OriginalFile),409,"CORRECTION_NEW_PDF_REQUIRED");
                    var archive=await api.Request(api.Customer,"GET","tickets/"+fixtures.GeneralTicket);checks.Check(archive["readOnly"]!.GetValue<bool>()&&archive["messages"]!.AsArray().Count==2,"real historical ticket and message rows remain readable as an authorized archive");
                    await api.Request(api.Other,"GET","tickets/"+fixtures.LinkedTicket,expected:404);
                    await fixtures.VerifyPrivateHistory(checks);
                }
                await OrderAttentionSqlScenarios.Run(api);
                await OrderWorkflowSqlScenarios.Run(api);
                await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Creation(),429);
                checks.Check(true,"real SQL intake rate limit remains enabled after twelve synthetic requests");
                // Separate fixture batches must not share a 15-minute synthetic intake window.
                // Expire only this disposable catalog's test limiter; application limits stay unchanged.
                await database.Write("UPDATE dbo.Rate_Limits SET expires=0 WHERE [key] LIKE 'orders:%'");
                await OrderConcurrencySqlScenarios.Run(api);
                await OrderRealtimeSqlScenarios.Run(api);
                await OrderIntegritySqlChecks.Run(api);
            }
            catch(Exception error)
            {
                failure=error is SqlException sql?"SqlException "+sql.Number+": "+sql.Message:error.GetType().Name+": "+error.Message;
                (database.Created?failures:blocked).Add(label+": "+failure);Console.WriteLine((database.Created?"FAIL ":"BLOCKED ")+label+": "+failure);
            }
            finally
            {
                try{privateRemoved=fixtures.CleanupPrivateStorage();}
                catch(Exception error){failures.Add(label+" private cleanup: "+error.Message);}
                try{await database.DisposeAsync();}
                catch(Exception error){failures.Add(label+" database cleanup: "+error.Message);Console.WriteLine("FAIL isolated cleanup unconfirmed");}
                phases.Add(new {label,instance=database.Instance,database=database.Name,engine,checks=checks.Passed.Count-before,failure,removed=database.Removed,privateRemoved});
            }
        }
        checks.Check(inventory.All(file=>Checksum(Path.Combine(MigrationRoot,file.name))==file.checksum),"all source migration files remain unchanged");
        File.WriteAllText(Path.Combine(ArtifactRoot,"sql-result.json"),JsonSerializer.Serialize(new {passed=checks.Passed.Count,failed=failures.Count,blocked=blocked.Count,checks=checks.Passed,failures,blockers=blocked,phases,inventory,operationalDatabaseAccessed=false,realSms=false,published=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SQL acceptance passed={checks.Passed.Count} failed={failures.Count} blocked={blocked.Count}");
        return failures.Count>0?1:blocked.Count>0?3:0;
    }
    internal static string Checksum(string file)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n","\n"))));
    private static async Task<string> History(IsolatedDatabase database)=>(await database.Read("SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(name,checksum,CONVERT(varchar(30),applied_at,126))),N'|') WITHIN GROUP(ORDER BY name) snapshot FROM dbo.Schema_Migrations"))!.Str("snapshot");
}
