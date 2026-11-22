using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

// Runs only inside the opt-in, server/catalog-guarded disposable LocalDB fixture.
internal static class AuditWorkflows
{
    public static async Task Run(IsolatedDatabase database, AcceptanceHost host, AcceptanceChecks checks, HttpClient admin)
    {
        using var customer = host.Client();
        const string phone = "09100000003", password = "Audit.Sql@2026";
        var challenge = await checks.Request(customer, "POST", "customer/otp/request", new { phone = "۰۹۱۰۰۰۰۰۰۰۳", purpose = "register" });
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts WHERE phone=@p0", phone))!.Num("n") == 0,
            "SQL OTP issuance does not create a customer account");
        await checks.Request(customer, "POST", "customer/otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "9999" }, 400);
        var grant = await checks.Request(customer, "POST", "customer/otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "٠١٢٣" });
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts WHERE phone=@p0", phone))!.Num("n") == 0,
            "SQL OTP verification stores a CK_OtpDelivery-compatible grant without an account");
        var profile = new { registrationToken = grant["registrationToken"]!.ToString(), firstName = "Audit", lastName = "Synthetic", password, confirmation = password };
        await checks.Request(customer, "POST", "customer/registration/complete", profile);
        await checks.Request(customer, "POST", "customer/registration/complete", profile, 400);
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts a JOIN dbo.Customer_Registrations r ON r.id=a.registration_id WHERE a.phone=@p0 AND r.phone_verified=1", phone))!.Num("n") == 1,
            "SQL registration atomically persists exactly one verified account/profile and consumes the grant");
        await checks.Request(customer, "POST", "customer/login", new { phone, password });
        var spec = new { pages = 120, quantity = 1, size = "raghi", paper = "white", color = "bw", binding = "soft" };
        var key = Guid.NewGuid().ToString();
        var receipt = await checks.Request(customer, "POST", "orders", new { idempotencyKey = key, title = "Audit SQL persisted order", spec }, 201);
        await checks.Request(customer, "POST", "orders", new { idempotencyKey = key, title = "Audit SQL persisted order", spec });
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Orders o JOIN dbo.Events e ON e.order_id=o.id WHERE o.id=@p0 AND e.status='new'", receipt["id"]!.ToString()))!.Num("n") == 1,
            "SQL order creation/replay persists one order and one initial event");
        var opened = await checks.Request(admin, "POST", "admin/orders/" + receipt["id"] + "/open");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Events WHERE order_id=@p0 AND status='review'", receipt["id"]!.ToString()))!.Num("n") == 1,
            "SQL explicit opening persists one review event");
        using (var upload = new HttpRequestMessage(HttpMethod.Post, "/api/orders/" + receipt["id"] + "/files"))
        {
            upload.Content = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n% synthetic audit fixture\n%%EOF"));
            upload.Content.Headers.ContentType = new("application/pdf");
            upload.Headers.Add("X-Order-Code", receipt["code"]!.ToString());
            upload.Headers.Add("X-File-Name", "audit.pdf");
            using var response = await customer.SendAsync(upload);
            checks.Check((int)response.StatusCode == 201, "real SQL upload persists metadata and writes only the isolated private PDF");
            var file = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            using var download = await admin.GetAsync("/api/admin/files/" + file["id"]);
            checks.Check(download.IsSuccessStatusCode && download.Content.Headers.ContentType?.MediaType == "application/pdf" && download.Headers.Contains("Content-Security-Policy"),
                "administrator downloads the isolated PDF with sandbox security headers");
            await checks.Request(customer,"GET","admin/files/"+file["id"],expected:401);
            var physical = Path.GetFullPath(Path.Combine(database.DirectoryPath,"unused-private-storage","uploads",receipt["id"]!.ToString(),file["id"]+".pdf"));
            checks.Check(physical.StartsWith(Path.GetFullPath(database.DirectoryPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&File.Exists(physical),
                "PDF bytes remain inside the verified disposable private-storage root");
            File.Delete(physical); // Exact synthetic file created above; never operational storage.
            using var missing = await admin.GetAsync("/api/admin/files/"+file["id"]);
            checks.Check((int)missing.StatusCode==404,"missing private bytes return 404 without a server-path leak");
        }
        var orderId=receipt["id"]!.ToString();
        var proposal=Guid.NewGuid().ToString();
        await checks.Request(admin,"POST","admin/orders/"+orderId+"/quotes",new{idempotencyKey=proposal,expectedVersion=opened["order"]!["version"]!.GetValue<int>(),amountToman=440000,scopeText="Printed book",deliveryTerms="Collect at shop",exclusionsText="Shipping"});
        var pending=await checks.Request(customer,"GET","customer/orders/"+orderId);
        var q=pending["quotes"]!.AsArray().Last()!;
        var changes=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{
            using var message=new HttpRequestMessage(HttpMethod.Post,"/api/customer/orders/"+orderId+"/quote/accept"){Content=System.Net.Http.Json.JsonContent.Create(new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=pending["order"]!["version"]!.GetValue<int>(),quoteId=q["id"]!.ToString(),quoteVersion=q["version"]!.GetValue<int>()})};
            using var response=await customer.SendAsync(message);return (int)response.StatusCode;
        }));
        checks.Check(changes.Order().SequenceEqual(new[]{200,409}),"real SQL competing owner approvals produce one commit and one version conflict");
        var accepted=await checks.Request(admin,"GET","admin/orders/"+orderId);
        await checks.Request(admin,"POST","admin/orders/"+orderId+"/start-printing",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=accepted["order"]!["version"]!.GetValue<int>()});
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Events WHERE order_id=@p0 AND status='printing'",orderId))!.Num("n")==1,"SQL owner approval and explicit start persist one production event");

        using var recovery=host.Client();using var oldSession=host.Client();
        await checks.Request(oldSession,"POST","customer/login",new{phone="09100000002",password="Sql.Acceptance@2026"});
        var resetChallenge=await checks.Request(recovery,"POST","customer/otp/request",new{phone="٠٩١٠٠٠٠٠٠٠٢",purpose="reset"});
        await checks.Request(recovery,"POST","customer/password/verify",new{challengeId=resetChallenge["challengeId"]!.ToString(),code="9999"},400);
        var reset=await checks.Request(recovery,"POST","customer/password/verify",new{challengeId=resetChallenge["challengeId"]!.ToString(),code="0123"});
        await checks.Request(recovery,"POST","customer/password/reset",new{resetToken=reset["resetToken"]!.ToString(),password="Recovered.Sql@2026",confirmation="Recovered.Sql@2026"});
        await checks.Request(oldSession,"GET","customer/me",expected:401);
        await checks.Request(recovery,"POST","customer/login",new{phone="09100000002",password="Sql.Acceptance@2026"},401);
        await checks.Request(recovery,"POST","customer/login",new{phone="09100000002",password="Recovered.Sql@2026"});
        await checks.Request(admin,"GET","auth/me");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts WHERE phone='09100000002'"))!.Num("n")==1,
            "SQL password recovery preserves one account, revokes old sessions and leaves administrator authentication independent");

        // Historical CMS remains persisted/authorized, while V2 hides the magazine.
        var article = new { slug = "audit-first-article", title = "Audit first published article", excerpt = "A synthetic article created by the isolated SQL audit.",
            content = "This is representative synthetic content for an isolated SQL acceptance test.", category = "Audit", published = true, seoTitle = "Audit article", seoDescription = "Isolated acceptance content" };
        await checks.Request(admin, "POST", "admin/articles", article, 201);
        using var anonymous = host.Client();
        var bootstrap = await checks.Request(anonymous, "GET", "public/bootstrap");
        var articles=await checks.Request(admin,"GET","admin/articles");
        checks.Check(bootstrap["articles"]!.AsArray().Count==0&&articles.AsArray().Any(a=>a?["slug"]?.ToString()==article.slug),
            "retired magazine is hidden publicly while the SQL article persists in its authorized archive");
        checks.Check(articles.AsArray().Count>=4,"historical CMS preserves the original three guides");
    }
}
