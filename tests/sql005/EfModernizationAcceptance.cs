using System.Text.Json.Nodes;
using System.Net.Http.Json;
using ChapGiti.Application.Common;
using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ChapGiti.SqlAcceptanceTests;

internal static class EfModernizationAcceptance
{
    public static async Task<int> Run(bool express2025 = false)
    {
        var checks=new AcceptanceChecks();
        foreach(var upgrade in new[]{false,true})
        {
            await using var database=new IsolatedDatabase(express2025);await database.Create();
            var engine = await database.Read("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) engine_version, compatibility_level FROM sys.databases WHERE name=DB_NAME()") ?? throw new InvalidOperationException("Required SQL acceptance row was missing.");
            Console.WriteLine($"SQL_ENGINE={engine!.Str("engine_version")} COMPATIBILITY_LEVEL={engine.Num("compatibility_level")} SCENARIO={(upgrade ? "upgrade" : "fresh")}");
            foreach(var file in Directory.GetFiles("backend/ChapGiti.Infrastructure/Persistence/Migrations","*.sql").Order())database.Stage(Path.GetFileName(file));
            await database.Migrate();var fixtures=new OrderWorkflowFixtures(database);
            var articleId=Guid.NewGuid().ToString();const string legacyBody="Historical article content that must remain byte-for-byte unchanged.";
            if(upgrade)await database.Write("INSERT dbo.Articles(id,slug,title,excerpt,content,category,published,seo_title,seo_description,created_at,updated_at) VALUES(@p0,'legacy-kept',N'Legacy saved title',N'Legacy saved excerpt',@p1,N'Print',1,'','',SYSUTCDATETIME(),SYSUTCDATETIME())",articleId,legacyBody);
            await using(var db=database.Store())
            {
                await database.Guard(db);
                try{await db.Transaction<bool>(async()=>{await db.Execute("UPDATE dbo.Schema_Migrations SET checksum=REPLICATE('0',64) WHERE name='001_initial.sql'");await db.Execute(LegacyEfMigrations.ValidationSql);throw new InvalidOperationException("Checksum mismatch was accepted.");});}
                catch(SqlException e) when(e.Number==51101){checks.Check(true,"baseline rejects mismatched historical checksums without registering EF history");}
                try{await db.Transaction<bool>(async()=>{await db.Execute("DROP INDEX IX_OrderQuotes_History ON dbo.Order_Quotes");await db.Execute(LegacyEfMigrations.ValidationSql);throw new InvalidOperationException("Missing index was accepted.");});}
                catch(SqlException e) when(e.Number==51103){checks.Check(true,"baseline rejects a missing legacy index");}
                if(upgrade)
                {
                    await database.Write("CREATE TABLE dbo.Article_Slug_Aliases(acceptance_conflict int NOT NULL)");
                    try{await LegacyEfMigrations.Apply(db);throw new InvalidOperationException("Conflicting schema unexpectedly migrated.");}
                    catch(SqlException e) when(e.Number==2714){checks.Check(true,"conflicting new EF table causes a visible migration failure");}
                    checks.Check((await database.Read("SELECT CASE WHEN COL_LENGTH('dbo.Articles','publication_status') IS NULL THEN 1 ELSE 0 END rolledBack"))!.Num("rolledBack")==1,"failed EF migration rolls back earlier article column additions");
                    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%CatalogAndMagazine'"))!.Num("n")==0,"failed EF feature migration is never registered as applied");
                    await database.Write("DROP TABLE dbo.Article_Slug_Aliases");
                }
                await LegacyEfMigrations.Apply(db);
            }
            checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n")==9,"EF adoption preserves all nine legacy history records");
            checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.__EFMigrationsHistory WHERE MigrationId IN('20261117090230_Legacy009Baseline','20261117092337_CatalogAndMagazine')"))!.Num("n")==2,"validated baseline and additive catalog/magazine migration use separate EF history");
            checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Print_Catalog_Options"))!.Num("n")==80,"all 80 existing print options transfer without invented rates");
            checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Print_Catalog_Options WHERE is_default=1 AND active=1"))!.Num("n")==8,"every catalog group has one active default");
            if(upgrade){var old=await database.Read("SELECT content,publication_status,published_at,public_author_name FROM dbo.Articles WHERE id=@p0",articleId) ?? throw new InvalidOperationException("Required SQL acceptance row was missing.");checks.Check(old!.Str("content")==legacyBody&&old.Str("publication_status")=="published"&&old["published_at"]==null&&old["public_author_name"]==null,"upgrade preserves content/publication without inventing historical author/date");}
            var before=(await database.Read("SELECT (SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH) history"))!.Str("history");
            await using(var db=database.Store()){await database.Guard(db);await LegacyEfMigrations.Apply(db);}
            checks.Check((await database.Read("SELECT (SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH) history"))!.Str("history")==before,"migration rerun preserves exact EF history");
            if(!upgrade)
            {
                var export=new JsonObject{["articles"]=new JsonArray(new JsonObject{["id"]=Guid.NewGuid().ToString(),["slug"]="legacy-imported",["title"]="Imported historical article",["excerpt"]="Imported actual historical excerpt",["content"]="Imported historical content that must remain unchanged.",["category"]="Import",["published"]=1,["seo_title"]="",["seo_description"]="",["created_at"]="2025-01-01T00:00:00.000Z",["updated_at"]="2025-01-01T00:00:00.000Z"})}.ToJsonString();
                await using var import=database.Store();await database.Guard(import);await ChapGiti.Infrastructure.Persistence.Importer.Apply(import,export);await ChapGiti.Infrastructure.Persistence.Importer.Apply(import,export);
                var imported=await database.Read("SELECT publication_status,published_at,public_author_name FROM dbo.Articles WHERE slug='legacy-imported'") ?? throw new InvalidOperationException("Required SQL acceptance row was missing.");
                checks.Check(imported!.Str("publication_status")=="published"&&imported["published_at"]==null&&imported["public_author_name"]==null,"legacy article import remains compatible after EF adoption without invented dates/authors");
                checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Import_History"))!.Num("n")==1,"legacy import retry is idempotent after EF activation");
            }
            await fixtures.SeedIdentities();
            await using var host=await AcceptanceHost.Start(database);using var admin=host.Client();using var customer=host.Client();using var anon=host.Client();
            using(var registered=host.Client())
            {
                const string registeredPhone="09100000003";
                var challenge=await checks.Request(registered,"POST","customer/otp/request",new{phone="۰۹۱۰۰۰۰۰۰۰۳",purpose="register"});
                checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts WHERE phone=@p0",registeredPhone))!.Num("n")==0,"real SQL registration issuance creates no completed customer");
                await checks.Request(registered,"POST","customer/otp/verify",new{challengeId=challenge["challengeId"]!.ToString(),code="9999"},400);
                var grant=await checks.Request(registered,"POST","customer/otp/verify",new{challengeId=challenge["challengeId"]!.ToString(),code="٠١٢٣"});
                checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts WHERE phone=@p0",registeredPhone))!.Num("n")==0,"OTP verification issues only a purpose-bound grant before profile completion");
                var profile=new{registrationToken=grant["registrationToken"]!.ToString(),firstName="Synthetic",lastName="Registered",password=OrderWorkflowFixtures.Password,confirmation=OrderWorkflowFixtures.Password};
                await checks.Request(registered,"POST","customer/registration/complete",profile);
                await checks.Request(registered,"POST","customer/registration/complete",profile,400);
                await checks.Request(registered,"POST","customer/login",new{phone=registeredPhone,password=OrderWorkflowFixtures.Password});
                await checks.Request(registered,"GET","customer/me");
            }
            using(var recovery=host.Client())
            {
                var challenge=await checks.Request(recovery,"POST","customer/otp/request",new{phone=OrderWorkflowFixtures.OtherPhone,purpose="reset"});
                var grant=await checks.Request(recovery,"POST","customer/password/verify",new{challengeId=challenge["challengeId"]!.ToString(),code="0123"});
                const string resetPassword="Isolated.Recovery@2026";
                await checks.Request(recovery,"POST","customer/password/reset",new{resetToken=grant["resetToken"]!.ToString(),password=resetPassword,confirmation=resetPassword});
                await checks.Request(recovery,"POST","customer/login",new{phone=OrderWorkflowFixtures.OtherPhone,password=resetPassword});
            }
            await checks.Request(admin,"POST","auth/login",new{username="admin",password=OrderWorkflowFixtures.Password});
            await checks.Request(customer,"POST","customer/login",new{phone=OrderWorkflowFixtures.Phone,password=OrderWorkflowFixtures.Password});
            await checks.Request(anon,"GET","admin/print-catalog",expected:401);await checks.Request(customer,"GET","admin/articles",expected:401);
            var catalog=await checks.Request(anon,"GET","public/print-catalog");checks.Check(catalog["items"]!.AsArray().Count==80,"public catalog projects current active SQL rows");
            var row=catalog["items"]!.AsArray().Single(n=>n!["kind"]!.ToString()=="bookSizes"&&n["code"]!.ToString()=="raghi")!.DeepClone().AsObject();row["label"]="SQL renamed format";
            await checks.Request(admin,"PUT","admin/print-catalog/"+row["id"],row);await checks.Request(admin,"PUT","admin/print-catalog/"+row["id"],row,409);
            var concurrent=(await checks.Request(admin,"GET","admin/print-catalog"))["items"]!.AsArray().Single(i=>i!["id"]!.ToString()==row["id"]!.ToString())!.DeepClone();
            var competing=await Task.WhenAll(admin.PutAsJsonAsync("/api/admin/print-catalog/"+row["id"],concurrent),admin.PutAsJsonAsync("/api/admin/print-catalog/"+row["id"],concurrent));
            checks.Check(competing.Select(r=>(int)r.StatusCode).Order().SequenceEqual(new[]{200,409}),"concurrent real SQL catalog edits permit exactly one writer");foreach(var response in competing)response.Dispose();
            checks.Check((await database.Read("SELECT label FROM dbo.Print_Catalog_Options WHERE id=@p0",row["id"]!.ToString()))!.Str("label")=="SQL renamed format","catalog update is durable and stale edits are rejected");
            var invalid=row.DeepClone().AsObject();invalid["code"]="unsupported-mode";invalid["kind"]="textPrints";invalid["pricingRule"]="invented-technology";await checks.Request(admin,"POST","admin/print-catalog",invalid,400);
            var tariffPath="admin/print-tariffs/"+row["id"];
            var tariff=await checks.Request(admin,"GET",tariffPath);
            await checks.Request(admin,"PUT",tariffPath,new{version=tariff["version"],rates=tariff["rates"],approveProposal=true});
            var spec=new{pages=120,quantity=100,size="raghi",paper="white",color="bw",binding="soft"};await checks.Request(anon,"POST","quote",spec,401);var quote=await checks.Request(customer,"POST","quote",spec);checks.Check(quote["total"]!.GetValue<long>()>0,"EF-backed pricing retains authoritative supported formulas");
            var intake=OrderWorkflowApi.Creation("book-printing");intake["termsAccepted"]=true;intake["spec"]=System.Text.Json.JsonSerializer.SerializeToNode(spec);
            using var content=OrderWorkflowApi.Multipart(intake,OrderWorkflowFixtures.Pdf());
            using var submitted=await customer.PostAsync("/api/orders",content);
            checks.Check((int)submitted.StatusCode==201,"authenticated order with a real PDF and approved tariff is created");
            var request=(await submitted.Content.ReadFromJsonAsync<JsonObject>())!;
            var saved=await database.Read("SELECT spec FROM dbo.Orders WHERE id=@p0",request["id"]!.ToString());checks.Check(JsonNode.Parse(saved!.Str("spec"))!["catalogSnapshot"]!["options"]!.AsArray().Any(i=>i!["label"]!.ToString()=="SQL renamed format"),"new order captures catalog identities/versions/labels");
            await checks.Request(admin,"GET","admin/users");await checks.Request(customer,"GET","customer/orders");await checks.Request(admin,"GET","admin/overview");
            var article=new JsonObject{["slug"]="ef-magazine",["title"]="EF magazine test title",["excerpt"]="A synthetic magazine summary for isolated acceptance.",["content"]="## Safe heading\n\nSynthetic article content sufficiently long for publication.",["category"]="Print",["status"]="draft",["coverAssetId"]="",["coverAlt"]="",["authorName"]="",["seoTitle"]="",["seoDescription"]=""};
            var created=await checks.Request(admin,"POST","admin/articles",article,201);var id=created["id"]!.ToString();await checks.Request(anon,"GET","public/articles/ef-magazine",expected:404);
            var edit=(await checks.Request(admin,"GET","admin/articles/"+id)).AsObject();edit["status"]="published";await checks.Request(admin,"PUT","admin/articles/"+id,edit);
            var detail=await checks.Request(anon,"GET","public/articles/ef-magazine");var published=detail["article"]!["publishedAt"]!.ToString();
            edit=(await checks.Request(admin,"GET","admin/articles/"+id)).AsObject();edit["title"]="Edited title without implicit slug change";edit["slug"]="ef-magazine-renamed";await checks.Request(admin,"PUT","admin/articles/"+id,edit);
            detail=await checks.Request(anon,"GET","public/articles/ef-magazine");checks.Check(detail["isAlias"]!.GetValue<bool>()&&detail["canonicalSlug"]!.ToString()=="ef-magazine-renamed"&&detail["article"]!["publishedAt"]!.ToString()==published,"old URLs and first publication date survive explicit slug edits");
            await checks.Request(admin,"PUT","admin/articles/"+id,edit,409);
            edit=(await checks.Request(admin,"GET","admin/articles/"+id)).AsObject();edit["status"]="draft";await checks.Request(admin,"PUT","admin/articles/"+id,edit);
            await checks.Request(anon,"GET","public/articles/ef-magazine-renamed",expected:404);await checks.Request(anon,"GET","public/articles/ef-magazine",expected:404);
            checks.Check((await checks.Request(anon,"GET","public/articles"))["items"]!.AsArray().All(a=>a!["id"]!.ToString()!=id),"unpublish hides list/detail/aliases immediately");
            var sitemap=await checks.Request(anon,"GET","public/magazine/sitemap");
            checks.Check(sitemap.AsArray().All(a=>a!["slug"]!.ToString()!="ef-magazine-renamed"),"real SQL sitemap never exposes an unpublished article or its aliases");
            await using(var db=database.Store()){await database.Guard(db);try{await db.Transaction<bool>(async()=>{await db.Context.Articles.Where(a=>a.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Title,"Must roll back"));throw new Rollback();});}catch(Rollback){}}
            checks.Check((await database.Read("SELECT title FROM dbo.Articles WHERE id=@p0",id))!.Str("title")!="Must roll back","EF writes roll back in the shared unit of work");
            checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.foreign_keys WHERE is_not_trusted=1 OR is_disabled=1"))!.Num("n")==0,"legacy/new foreign keys remain enabled and trusted");
        }
        Console.WriteLine(checks.Passed.Count+" EF/catalog/magazine real SQL checks passed; no operational database or real SMS was used.");return 0;
    }
    private sealed class Rollback:Exception;
}
