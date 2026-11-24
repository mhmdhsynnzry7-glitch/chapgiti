using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ChapGiti.AuthenticationTests;

internal static class RegistrationStorageScenarios
{
    private const string LegacyInsert = """
        INSERT dbo.Customer_Otp_Challenges(id,phone,purpose,created_at,expires,delivery_status)
        VALUES(@p0,@p1,'register',@p2,@p3,'grant')
        """;

    public static async Task Run(Action<bool, string> check)
    {
        var sql = new SchemaCheckedOtpSql();
        try
        {
            await sql.Execute(LegacyInsert, new string('b',64), AuthFixture.Phone, 1000L, 301000L);
            throw new InvalidOperationException("The old grant state bypassed the deployed CHECK constraint.");
        }
        catch (SqlException error)
        {
            check(error.Number == 547 && error.Message.Contains("CK_OtpDelivery"),
                "isolated schema reproduction: legacy grant delivery status violates CK_OtpDelivery with SQL 547");
        }

        var repository = new OtpRepository(sql);
        var hash = new string('b',64); var sms = new string('a',64);
        sql.SeedSms(sms, AuthFixture.Phone, 1000);
        await repository.AddRegistrationGrant(hash, AuthFixture.Phone, 301000, 2000);
        var grant = await repository.FindRegistrationGrant(hash);
        var logical = await repository.Find(hash);
        check(grant?.Phone == AuthFixture.Phone && grant.Expires == 301000 && !grant.Consumed &&
            logical?.DeliveryStatus == "grant" && logical.CodeHash == null && logical.Id.Length == 64,
            "production grant INSERT satisfies original SQL checks and maps to the logical grant without losing token digest bits");
        check(await repository.CountRequests(AuthFixture.Phone, 0) == 1 && (await repository.Latest(AuthFixture.Phone))?.Id == sms,
            "actual repository SQL excludes encoded grants from SMS quotas and resend cooldown");
        check(await repository.ConsumeRegistrationGrant(hash, 3000) && !await repository.ConsumeRegistrationGrant(hash, 4000),
            "encoded grant consumption is conditional and one-time");
        check((await repository.FindRegistrationGrant(hash))!.Consumed && await repository.CountRequests(AuthFixture.Phone, 0) == 1,
            "consumed grants retain their storage tag and never become SMS requests");
        var replacement = new string('d',64);
        await repository.AddRegistrationGrant(replacement, AuthFixture.Phone, 302000, 4000);
        await repository.Invalidate(AuthFixture.Phone, 5000);
        check((await repository.FindRegistrationGrant(replacement))!.Consumed && await repository.CountRequests(AuthFixture.Phone, 0) == 1 &&
            (await repository.Latest(AuthFixture.Phone))?.Id == sms,
            "phone invalidation revokes encoded grants while preserving their quota exclusion");
        var expiring = new string('e',64);
        await repository.AddRegistrationGrant(expiring, AuthFixture.Phone, 6000, 5000);
        check(!await repository.ConsumeRegistrationGrant(expiring, 6000), "repository SQL rejects grants at their exact expiry");
        check(await repository.FindRegistrationGrant(sms) == null, "an ordinary SMS challenge cannot be read as a registration grant");

        await ApiRegression(check);
    }

    private static async Task ApiRegression(Action<bool, string> check)
    {
        using var captured = new CapturedSqlErrors();
        await using var host = await TestApiHost.Start(errorLogger: captured);
        using var client = new HttpClient(new HttpClientHandler { UseCookies=false }) { BaseAddress=new Uri(host.Origin) };
        async Task<JsonObject> Post(string path, object body, HttpStatusCode status=HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,"/api/customer/"+path) { Content=JsonContent.Create(body) };
            request.Headers.Add("Origin",host.Origin);
            using var response = await client.SendAsync(request);
            check(response.StatusCode==status,"schema-aware registration API "+path+" -> "+(int)status);
            return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        }
        const string phone="09177777777";
        var challenge=await Post("otp/request",new {phone,purpose="register"});
        var id=challenge["challengeId"]!.ToString();
        host.Fixture.Store.GrantStorageCheck=(hash,mobile,expires,now)=>new SchemaCheckedOtpSql().Execute(LegacyInsert,hash,mobile,now,expires);
        var error=await Post("otp/verify",new {challengeId=id,code="0123"},HttpStatusCode.InternalServerError);
        check(captured.Numbers.SequenceEqual(new[]{547}) && error["error"]!.ToString()=="در پردازش درخواست مشکلی پیش آمد. دوباره تلاش کنید." &&
            !error.ContainsKey("registrationToken") && !error.ToJsonString().Contains("CK_OtpDelivery"),
            "actual error middleware logs simulated SQL 547 and reproduces the reported sanitized HTTP 500");
        check(!host.Fixture.Store.Challenges[id].Consumed && host.Fixture.Store.Challenges[id].CodeHash!=null &&
            !host.Fixture.Store.Accounts.ContainsKey(phone),
            "failed grant INSERT rolls back OTP consumption without creating a customer");
        host.Fixture.Store.GrantStorageCheck=null;
        var grant=await Post("otp/verify",new {challengeId=id,code="۰۱۲۳"});
        check(grant.Count==4 && grant["phone"]!.ToString()==phone && !host.Fixture.Store.Accounts.ContainsKey(phone) && host.Fixture.Sender.Sends==1,
            "the same OTP retries successfully with schema-compatible storage, no extra SMS and no premature account");
        var stored=host.Fixture.Store.Challenges.Values.Single(row=>row.DeliveryStatus=="grant");
        var rejected=await Post("otp/verify",new {challengeId=stored.Id,code="0123"},HttpStatusCode.BadRequest);
        check(rejected["code"]?.ToString()=="OTP_CHALLENGE_INVALID", "a registration grant can never be verified as an OTP challenge");
        await Post("registration/complete",AuthFixture.RegistrationBody(grant));
        await Post("login",new {phone,password=AuthFixture.Password});
        check(host.Fixture.Store.Accounts.ContainsKey(phone), "schema-aware OTP to profile to account to password login regression completes");
    }

    private sealed class CapturedSqlErrors : ILoggerProvider
    {
        internal List<int> Numbers {get;}=[];
        public ILogger CreateLogger(string categoryName)=>new Capture(this);
        public void Dispose() { }
        private sealed class Capture(CapturedSqlErrors owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
            public bool IsEnabled(LogLevel level)=>level>=LogLevel.Error;
            public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
            { if (exception != null) throw new InvalidOperationException("Raw exception reached error output");
              if (state is IEnumerable<KeyValuePair<string,object?>> fields && fields.FirstOrDefault(f=>f.Key=="SqlNumber").Value is int number) owner.Numbers.Add(number); }
        }
    }
}
