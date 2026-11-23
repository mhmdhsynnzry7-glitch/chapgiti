using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Api.Controllers;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Common;

namespace ChapGiti.SqlAcceptanceTests;

// Explicit disposable acceptance; it never reads configured/operational connection strings.
internal static class EfActivationAcceptance
{
    private static readonly string Artifacts = Path.Combine(".qa", "ef-activation-" + Guid.NewGuid().ToString("N"));
    private static readonly string[] ExpectedEf = [
        "20261117090230_Legacy009Baseline", "20261117092337_CatalogAndMagazine",
        "20261118113729_TicketMessageReadState", "20261122130922_CutTariffVersions", "20261123062444_ProposedCutTariffs"
    ];
    private static readonly string[] LegacyTables = [
        "Customer_Registrations", "Customer_Accounts", "Admin_Users", "Admin_Usernames",
        "Orders", "Order_Quotes", "Order_Corrections", "Files", "Notifications", "Order_Views",
        "Tickets", "Ticket_Messages", "Settings", "Events", "Schema_Migrations"
    ];

    public static async Task<int> Run(bool express2025 = false)
    {
        if (!File.Exists("AGENTS.md")) throw new InvalidOperationException("Run from the repository root.");
        Directory.CreateDirectory(Artifacts);
        Console.WriteLine("Activation evidence: " + Artifacts);
        var checks = new AcceptanceChecks();
        var results = new List<object>();
        foreach (var upgrade in new[] { false, true })
        {
            var database = new IsolatedDatabase(express2025);
            var fixtures = new OrderWorkflowFixtures(database);
            var scenario = upgrade ? "upgrade-legacy009-to-ef" : "fresh-complete-maintenance-command";
            var before = checks.Passed.Count;
            string? failure = null;
            Dictionary<string, object?>? engine = null;
            var privateRemoved = false;
            try
            {
                await database.Create();
                engine = await database.Read("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) engine_version,compatibility_level,collation_name FROM sys.databases WHERE name=DB_NAME()")
                    ?? throw new InvalidOperationException("Isolated engine identity is unavailable.");
                Console.WriteLine($"ACTIVATION_SCENARIO={scenario} SQL_ENGINE={engine.Str("engine_version")} COMPATIBILITY_LEVEL={engine.Num("compatibility_level")} COLLATION={engine.Str("collation_name")}");
                Dictionary<string, string>? history = null;
                if (upgrade)
                {
                    foreach (var file in Directory.GetFiles("backend/ChapGiti.Infrastructure/Persistence/Migrations", "*.sql").Order())
                        database.Stage(Path.GetFileName(file));
                    await database.Migrate();
                    await fixtures.SeedHistorical();
                    await SeedRichHistory(database, fixtures);
                    history = await Snapshot(database);
                    var pending = await Command(database, "--check");
                    checks.Check(pending.ExitCode != 0 && pending.Output.Contains("[MIGRATION_REQUIRED] Pending EF:") &&
                        pending.Output.Contains("CatalogAndMagazine"), "startup preflight requires all compiled EF migrations on a legacy-only catalog");
                    checks.Check((await database.Read("SELECT OBJECT_ID('dbo.__EFMigrationsHistory') id"))!["id"] == null,
                        "read-only EF preflight leaves missing EF history untouched");

                    // Repair only this owned test index after exercising the real CLI failure.
                    await database.Write("DROP INDEX IX_OrderQuotes_History ON dbo.Order_Quotes");
                    var rejected = await Command(database, "--migrate");
                    checks.Check(rejected.ExitCode != 0 && rejected.Output.Contains("51103"), "real maintenance command rejects incompatible baseline prerequisites with SQL51103");
                    checks.Check((await database.Read("SELECT CASE WHEN OBJECT_ID('dbo.__EFMigrationsHistory') IS NULL THEN 1 ELSE 0 END n"))!.Num("n") == 1,
                        "incompatible baseline is never registered and no EF history table is created");
                    await database.Write("CREATE INDEX IX_OrderQuotes_History ON dbo.Order_Quotes(order_id,revision DESC)");
                    await database.Write("CREATE TABLE dbo.Article_Slug_Aliases(acceptance_conflict int NOT NULL)");
                    var incomplete = await Command(database, "--migrate");
                    checks.Check(incomplete.ExitCode != 0 && incomplete.Output.Contains("2714"), "feature DDL failure is reported after registration of the validated baseline");
                    var featurePending = await Command(database, "--check");
                    checks.Check(featurePending.ExitCode != 0 && featurePending.Output.Contains("[MIGRATION_REQUIRED] Pending EF: " + string.Join(", ", ExpectedEf.Skip(1)) + "."),
                        "baseline-only history reports every pending compiled EF migration in order");
                    await database.Write("DROP TABLE dbo.Article_Slug_Aliases");
                }
                else
                {
                    var readiness = await Command(database, "--check");
                    checks.Check(readiness.ExitCode != 0 && readiness.Output.Contains("[MIGRATION_REQUIRED] Pending legacy SQL:") && readiness.Output.Contains("009_order_views.sql"), "empty database reports pending legacy chain");
                    var production = await Command(database, null, "Production");
                    checks.Check(production.ExitCode != 0 && production.Output.Contains("[MIGRATION_REQUIRED]"), "direct production startup refuses the empty catalog before serving or migrating");
                    checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.tables"))!.Num("n") == 0,
                        "read-only check never creates migration history or schema on an empty database");
                }

                var applied = await Command(database, "--migrate");
                checks.Check(applied.ExitCode == 0, scenario + " supported --migrate command succeeds");
                await VerifySchema(database, checks);
                if (upgrade)
                {
                    var after = await Snapshot(database);
                    foreach (var (table, digest) in history!)
                        checks.Check(after[table] == digest, "EF adoption preserves all existing " + table + " values/identifiers byte-for-byte");
                    await fixtures.VerifyPrivateHistory(checks);
                    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Articles WHERE (slug='activation-legacy-published' AND publication_status='published') OR (slug='activation-legacy-draft' AND publication_status='draft')"))!.Num("n") == 2,
                        "legacy publication flags retain their meaning without publishing drafts");
                    checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Articles WHERE published_at IS NOT NULL OR public_author_name IS NOT NULL"))!.Num("n") == 0,
                        "baseline activation creates no fabricated publication dates or authors");
                }
                else
                {
                    var readiness = await Command(database, "--check");
                    checks.Check(readiness.ExitCode != 0 && readiness.Output.Contains("No administrator is configured."),
                        "fresh schema check distinguishes missing administrator from migration failure");
                    await fixtures.SeedIdentities();
                }
                checks.Check((await Command(database, "--check")).ExitCode == 0, "maintenance check accepts existing synthetic administrator without resetting credentials");
                if (!upgrade)
                {
                    // Introduce physical drift only on this disposable database; histories remain untouched.
                    await database.Write("DROP INDEX IX_OrderQuotes_History ON dbo.Order_Quotes");
                    var drift = await Command(database, "--check");
                    checks.Check(drift.ExitCode != 0 && drift.Output.Contains("51103"), "startup rejects physical legacy index drift even with complete histories");
                    await database.Write("CREATE INDEX IX_OrderQuotes_History ON dbo.Order_Quotes(order_id,revision DESC)");
                }
                await VerifyCatalogDataset(database, checks);

                await using (var host = await AcceptanceHost.Start(database))
                {
                    using var admin = host.Client();
                    using var customer = host.Client();
                    using var guest = host.Client();
                    await checks.Request(admin, "POST", "auth/login", new { username = "admin", password = OrderWorkflowFixtures.Password });
                    await checks.Request(customer, "POST", "customer/login", new { phone = OrderWorkflowFixtures.Phone, password = OrderWorkflowFixtures.Password });
                    await checks.Request(customer, "GET", "admin/print-catalog", expected: 401);
                    await checks.Request(admin, "GET", "customer/me", expected: 401);
                    var catalog = await checks.Request(admin, "GET", "admin/print-catalog");
                    var option = catalog["items"]!.AsArray().Single(item => item!["kind"]!.ToString() == "bookSizes" && item["code"]!.ToString() == "raghi")!.DeepClone().AsObject();
                    option["label"] = "Activation SQL format";
                    option["displayOrder"] = 7;
                    option["metadata"]!["width"] = 15;
                    option["metadata"]!["height"] = 22;
                    await checks.Request(admin, "PUT", "admin/print-catalog/" + option["id"], option);
                    var publicCatalog = await checks.Request(guest, "GET", "public/print-catalog");
                    checks.Check(publicCatalog["items"]!.AsArray().Any(item => item!["id"]!.ToString() == option["id"]!.ToString() && item["label"]!.ToString() == "Activation SQL format" && item["metadata"]!["width"]!.GetValue<int>() == 15),
                        "persisted administrator catalog edits reach the real customer-facing SQL API");
                    var nextDefault = catalog["items"]!.AsArray().Single(item => item!["kind"]!.ToString() == "bookSizes" && item["code"]!.ToString() == "vaziri")!.DeepClone().AsObject();
                    nextDefault["isDefault"] = true;
                    await checks.Request(admin, "PUT", "admin/print-catalog/" + nextDefault["id"], nextDefault);
                    publicCatalog = await checks.Request(guest, "GET", "public/print-catalog");
                    checks.Check(publicCatalog["items"]!.AsArray().Count(item => item!["kind"]!.ToString() == "bookSizes" && item["isDefault"]!.GetValue<bool>()) == 1 &&
                        publicCatalog["items"]!.AsArray().Any(item => item!["code"]!.ToString() == "vaziri" && item["kind"]!.ToString() == "bookSizes" && item["isDefault"]!.GetValue<bool>()),
                        "real SQL default changes clear the former default and expose exactly one new active default");
                    var editedCatalog = await checks.Request(admin, "GET", "admin/print-catalog");
                    var restoredDefault = editedCatalog["items"]!.AsArray().Single(item => item!["id"]!.ToString() == option["id"]!.ToString())!.DeepClone().AsObject();
                    restoredDefault["isDefault"] = true;
                    await checks.Request(admin, "PUT", "admin/print-catalog/" + restoredDefault["id"], restoredDefault);
                    var inactive = editedCatalog["items"]!.AsArray().Single(item => item!["kind"]!.ToString() == "bookSizes" && item["code"]!.ToString() == "a4")!.DeepClone().AsObject();
                    inactive["active"] = false;
                    await checks.Request(admin, "PUT", "admin/print-catalog/" + inactive["id"], inactive);
                    publicCatalog = await checks.Request(guest, "GET", "public/print-catalog");
                    checks.Check(publicCatalog["items"]!.AsArray().All(item => item!["id"]!.ToString() != inactive["id"]!.ToString()),
                        "deactivated SQL options disappear from public choices without deleting historical catalog rows");
                    var orderedSizes = publicCatalog["items"]!.AsArray().Where(item => item!["kind"]!.ToString() == "bookSizes").ToArray();
                    checks.Check(orderedSizes.Select(item => item!["displayOrder"]!.GetValue<int>()).SequenceEqual(orderedSizes.Select(item => item!["displayOrder"]!.GetValue<int>()).Order()),
                        "customer catalog preserves administrator display ordering from SQL");
                    if (upgrade)
                    {
                        var bootstrap = await checks.Request(guest, "GET", "public/bootstrap");
                        var legacySettings = await checks.Request(admin, "GET", "admin/settings");
                        checks.Check(bootstrap["rates"] == null && legacySettings["rates"]!["setup"] == null && (await database.Read("SELECT JSON_VALUE([value],'$.setup') amount FROM dbo.Settings WHERE [key]='rates'"))!.Str("amount") == "87654",
                            "historical reference rates stay in storage and are absent from current rate responses");
                    }
                    var cutPath = "admin/print-tariffs/" + option["id"];
                    var unset = await checks.Request(admin, "GET", cutPath);
                    checks.Check(unset["version"]!.GetValue<int>() == 1 && unset["isProposed"]!.GetValue<bool>() && unset["rates"]!.AsObject().Count == 11,
                        "fresh and upgraded fixed cuts receive complete unapproved proposals");
                    var sampleRates = ChapGiti.Domain.Printing.CutTariff.Keys.ToDictionary(key => key, _ => 10);
                    await checks.Request(admin, "PUT", cutPath, new { version = 1, approveProposal = true, rates = sampleRates });
                    var stored = await checks.Request(admin, "GET", cutPath);
                    var otherCut = await checks.Request(admin, "GET", "admin/print-tariffs/" + nextDefault["id"]);
                    checks.Check(stored["version"]!.GetValue<int>() == 2 && stored["rates"]!["setup"]!.GetValue<int>() == 10 &&
                        otherCut["version"]!.GetValue<int>() == 1 && otherCut["isProposed"]!.GetValue<bool>(), "an explicit test rate revision persists only for the selected cut");
                    await checks.Request(admin, "PUT", cutPath, new { version = 0, rates = sampleRates }, 409);
                    var invalid = option.DeepClone().AsObject();
                    invalid["code"] = "activation-unsupported";
                    invalid["pricingRule"] = "arbitrary-executable-price";
                    await checks.Request(admin, "POST", "admin/print-catalog", invalid, 400);
                    await VerifyMagazine(database, checks, admin, guest);
                    var beforeRerun = await Snapshot(database, includeFeatures: true);
                    checks.Check((await Command(database, "--migrate")).ExitCode == 0, "supported complete maintenance procedure is safe to repeat after administrator edits");
                    var afterRerun = await Snapshot(database, includeFeatures: true);
                    foreach (var (table, digest) in beforeRerun)
                        checks.Check(afterRerun[table] == digest, "rerun preserves edited " + table + " rows and timestamps without duplicate initialization");

                    if (!upgrade) await Browser(database, host, checks);
                    if (upgrade)
                    {
                        var preserved = await Snapshot(database);
                        foreach (var (table, digest) in history!.Where(pair => pair.Key is not ("Articles" or "Schema_Migrations")))
                            checks.Check(preserved[table] == digest, "catalog/magazine activity does not reprice or rewrite historical " + table);
                        await fixtures.VerifyPrivateHistory(checks);
                        await checks.Request(customer, "GET", "tickets/" + fixtures.GeneralTicket);
                        await checks.Request(customer, "POST", "tickets", new { subject = "Retired intake", message = "Must never persist" }, 410);
                        await checks.Request(admin, "PATCH", "admin/tickets/" + fixtures.LinkedTicket, new { status = "open" }, 410);
                    }
                }
            }
            catch (Exception error)
            {
                failure = error.GetType().Name + ": " + error.Message;
                Console.WriteLine("FAIL " + scenario + " " + failure);
            }
            finally
            {
                try { privateRemoved = fixtures.CleanupPrivateStorage(); await database.DisposeAsync(); }
                catch (Exception error) { failure = (failure ?? "") + " cleanup: " + error.Message; }
                results.Add(new { scenario, passed = checks.Passed.Count - before, failure, engine, databaseRemoved = database.Removed, privateRemoved, operationalAccess = false, realSms = false });
                File.WriteAllText(Path.Combine(Artifacts, "acceptance-result.json"), JsonSerializer.Serialize(new { checks = checks.Passed, scenarios = results }, new JsonSerializerOptions { WriteIndented = true }));
            }
            if (failure != null) return 1;
        }
        Console.WriteLine($"{checks.Passed.Count} EF activation acceptance checks passed; no operational access, publish or real SMS.");
        return 0;
    }

    private static async Task SeedRichHistory(IsolatedDatabase database, OrderWorkflowFixtures fixtures)
    {
        var now = Input.Now();
        var original = (await database.Read("SELECT id FROM dbo.Files WHERE order_id=@p0", fixtures.Accepted))!.Str("id");
        var replacement = await fixtures.HistoricalFile(fixtures.Accepted, "approved-existing.pdf", OrderWorkflowFixtures.Pdf("approved historical replacement"));
        var correction = Input.NewId();
        await database.Write("INSERT dbo.Order_Corrections(id,order_id,revision,state,is_active,request_text,requires_new_pdf,requested_by_admin,requested_at,is_legacy,base_file_id) VALUES(@p0,@p1,1,'requested',1,N'Known synthetic correction',1,@p2,@p3,0,@p4)", correction, fixtures.Accepted, OrderWorkflowFixtures.Administrator, now, original);
        await database.Write("UPDATE dbo.Files SET file_version=2,correction_id=@p0 WHERE id=@p1", correction, replacement);
        await database.Write("UPDATE dbo.Order_Corrections SET state='approved',is_active=0,submission_text=N'Existing valid replacement',submitted_by_customer=@p0,submitted_at=@p1,file_id=@p2,reviewed_by_admin=@p3,reviewed_at=@p1,approved_file_id=@p2 WHERE id=@p4", fixtures.Owner, now, replacement, OrderWorkflowFixtures.Administrator, correction);
        await database.Write("UPDATE dbo.Orders SET approved_file_id=@p0 WHERE id=@p1", replacement, fixtures.Accepted);
        var sequence = (await database.Read("SELECT MAX(sequence) n FROM dbo.Notifications"))!.Num("n");
        await database.Write("INSERT dbo.Order_Views(order_id,recipient_kind,customer_id,admin_email,seen_through_sequence,seen_at) VALUES(@p0,'customer',@p1,NULL,@p2,@p3),(@p0,'admin',NULL,@p4,@p2,@p3)", fixtures.Accepted, fixtures.Owner, sequence, now, OrderWorkflowFixtures.Administrator);
        var rates = JsonNode.Parse(File.ReadAllText("backend/ChapGiti.Infrastructure/Content/defaults.json"))!["rates"]!.DeepClone();
        rates["setup"] = 87654;
        await database.Write("INSERT dbo.Settings([key],[value]) VALUES(N'rates',@p0)", rates.ToJsonString());
        foreach (var published in new[] { false, true })
            await database.Write("INSERT dbo.Articles(id,slug,title,excerpt,content,category,published,seo_title,seo_description,created_at,updated_at) VALUES(@p0,@p1,N'Saved legacy article',N'Saved legacy summary',N'Saved historical content remains unchanged.',N'Legacy',@p2,'','',@p3,@p3)", Input.NewId(), published ? "activation-legacy-published" : "activation-legacy-draft", published, now);
    }

    private static async Task<Dictionary<string, string>> Snapshot(IsolatedDatabase database, bool includeFeatures = false)
    {
        var snapshots = new Dictionary<string, string>();
        foreach (var table in LegacyTables.Concat(includeFeatures ? new[] { "Print_Catalog_Options", "Cut_Tariff_Versions", "Article_Slug_Aliases", "__EFMigrationsHistory" } : []))
        {
            // Table names are the fixed allowlist above; only hashes of synthetic data leave this method.
            var order = table switch { "Customer_Accounts" => "phone", "Admin_Users" => "email", "Admin_Usernames" => "username", "Settings" => "[key]", "Schema_Migrations" => "name", "__EFMigrationsHistory" => "MigrationId", "Article_Slug_Aliases" => "slug", "Cut_Tariff_Versions" => "cut_id,version", _ => "id" };
            var projection = table == "Ticket_Messages" && !includeFeatures ? "id,ticket_id,actor,message,created_at" : "*";
            var json = (await database.Read($"SELECT (SELECT {projection} FROM dbo.[{table}] ORDER BY {order} FOR JSON PATH,INCLUDE_NULL_VALUES) snapshot"))!.Str("snapshot");
            snapshots[table] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
        }
        var columns = includeFeatures ? "*" : "id,slug,title,excerpt,content,category,published,seo_title,seo_description,created_at,updated_at";
        var articles = (await database.Read($"SELECT (SELECT {columns} FROM dbo.Articles ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES) snapshot"))!.Str("snapshot");
        snapshots["Articles"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(articles)));
        return snapshots;
    }

    private static async Task VerifySchema(IsolatedDatabase database, AcceptanceChecks checks)
    {
        foreach (var file in Directory.GetFiles("backend/ChapGiti.Infrastructure/Persistence/Migrations", "*.sql").Order())
            checks.Check((await database.Read("SELECT checksum FROM dbo.Schema_Migrations WHERE name=@p0", Path.GetFileName(file)))!.Str("checksum") == OrderWorkflowAcceptance.Checksum(file), "full maintenance command registers exact legacy checksum " + Path.GetFileName(file));
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Schema_Migrations"))!.Num("n") == 9, "legacy history has exactly nine entries");
        var ids = (await database.Read("SELECT (SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH) ids"))!.Str("ids");
        var applied = JsonNode.Parse(ids)!.AsArray().Select(row => row!["MigrationId"]!.GetValue<string>());
        checks.Check(applied.SequenceEqual(ExpectedEf), "all four reviewed EF migrations are registered in order");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1"))!.Num("n") == 0, "all historical and EF foreign keys are enabled/trusted");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')"))!.Num("n") == 36, "fresh/upgrade has the expected legacy, tariff, catalog and separate migration history tables");
    }

    private static async Task VerifyCatalogDataset(IsolatedDatabase database, AcceptanceChecks checks)
    {
        var expected = JsonNode.Parse(File.ReadAllText("backend/ChapGiti.Infrastructure/Persistence/Ef/InitialPrintCatalog.json"))!.AsArray();
        var stored = JsonNode.Parse((await database.Read("SELECT (SELECT id,kind,code,label,pricing_rule pricingRule,metadata,display_order displayOrder,active,is_default isDefault FROM dbo.Print_Catalog_Options FOR JSON PATH) items"))!.Str("items"))!.AsArray();
        foreach (var item in stored) item!["metadata"] = JsonNode.Parse(item["metadata"]!.ToString());
        checks.Check(expected.Count == stored.Count && expected.All(item => stored.Any(row => JsonNode.DeepEquals(item, row))),
            "all 80 initialized identities, rules, labels, metadata, defaults and ordering exactly match the approved source dataset");
    }

    private static async Task VerifyMagazine(IsolatedDatabase database, AcceptanceChecks checks, HttpClient admin, HttpClient guest)
    {
        var body = new JsonObject { ["slug"] = "activation-magazine", ["title"] = "Activation magazine title", ["excerpt"] = "A synthetic magazine summary for activation acceptance.", ["content"] = "## Safe content\n\nSynthetic article body with sufficient length for publication.", ["category"] = "Activation", ["status"] = "draft", ["coverAssetId"] = "", ["coverAlt"] = "", ["authorName"] = "", ["seoTitle"] = "", ["seoDescription"] = "" };
        var created = await checks.Request(admin, "POST", "admin/articles", body, 201);
        var id = created["id"]!.ToString();
        foreach (var status in new[] { "published", "draft", "archived" })
        {
            var edit = (await checks.Request(admin, "GET", "admin/articles/" + id)).AsObject();
            edit["status"] = status;
            await checks.Request(admin, "PUT", "admin/articles/" + id, edit);
            await checks.Request(guest, "GET", "public/articles/activation-magazine", expected: status == "published" ? 200 : 404);
        }
        var invalid = body.DeepClone().AsObject(); invalid["slug"] = "activation-invalid-content"; invalid["content"] = "<script>alert(1)</script>";
        await checks.Request(admin, "POST", "admin/articles", invalid, 400);
        invalid = body.DeepClone().AsObject(); invalid["slug"] = "activation-invalid-asset"; invalid["coverAssetId"] = Guid.NewGuid().ToString();
        await checks.Request(admin, "POST", "admin/articles", invalid, 400);
        checks.Check((await database.Read("SELECT publication_status FROM dbo.Articles WHERE id=@p0", id))!.Str("publication_status") == "archived", "archived article persists without a fabricated publication transition on rerun");
    }

    private static async Task<(int ExitCode, string Output)> Command(IsolatedDatabase database, string? flag, string environment = "Development")
    {
        await using (var store = database.Store()) await database.Guard(store);
        var assembly = typeof(PrintCatalogController).Assembly.Location;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var key in start.Environment.Keys.ToArray())
            if (System.Text.RegularExpressions.Regex.IsMatch(key, "^(SQLSERVER_|ConnectionStrings__|Storage__|Sms__|Otp__|OTP_|MELIPAYAMAK_|CHAP_UAT_|DATA_DIR$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) start.Environment.Remove(key);
        start.Environment["ConnectionStrings__SqlServer"] = database.ConnectionString();
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        start.Environment["DOTNET_ENVIRONMENT"] = environment;
        start.Environment["Site__Url"] = "http://127.0.0.1:3000";
        start.Environment["Sms__Enabled"] = "false";
        start.Environment["Otp__Enabled"] = "false";
        start.Environment["OTP_VERIFICATION_ENABLED"] = "true";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        start.ArgumentList.Add(assembly); if (flag != null) start.ArgumentList.Add(flag);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start maintenance command.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var captured = await output + await error;
        await File.AppendAllTextAsync(Path.Combine(Artifacts, "maintenance-cli.log"), flag + " exit=" + process.ExitCode + Environment.NewLine + captured + Environment.NewLine);
        return (process.ExitCode, captured);
    }

    private static async Task Browser(IsolatedDatabase database, AcceptanceHost host, AcceptanceChecks checks)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start(); var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var origin = "http://127.0.0.1:" + port;
        host.AllowIsolatedFrontend(origin);
        await using (var store = database.Store()) await database.Guard(store);
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("tests/sql005/ef-activation-browser.mjs"); start.ArgumentList.Add(host.Origin); start.ArgumentList.Add(origin);
        start.ArgumentList.Add(Artifacts);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start SQL-backed activation browser.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(100)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        await File.WriteAllTextAsync(Path.Combine(Artifacts, "sql-browser.log"), await output + await error);
        checks.Check(process.ExitCode == 0, "real SQL -> EF/API -> Next customer catalog and magazine browser acceptance succeeds");
    }
}
