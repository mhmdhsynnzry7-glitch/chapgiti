using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Domain.Authentication;
using ChapGiti.Infrastructure.Security;

namespace ChapGiti.AuthenticationTests;

internal static class IndependentAuthenticationScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        var cases = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"password-policy-cases.json")))!.AsArray();
        foreach (var item in cases)
        {
            var value = item!["value"]!.GetValue<string>();
            check(PasswordPolicy.IsValid(value) == item["valid"]!.GetValue<bool>(), "backend shared password policy: " + item["name"]);
        }
        check(PasswordPolicy.Confirm("A!1234","A!1234")=="A!1234", "exactly six-character passwords are accepted on creation");
        try { PasswordPolicy.Confirm("A!1234","B!1234"); throw new Exception("Mismatch accepted."); }
        catch (ChapGiti.Domain.Common.RuleViolation) { check(true,"password confirmation is mandatory"); }
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("A!1234");
        check(hash != "A!1234" && hasher.Verify("A!1234",hash), "new policy passwords use the existing salted PBKDF2 hasher");
        // Build historical credentials independently of the creation API: complexity is not retroactive.
        const string historical = "legacy";
        var salt = new byte[16];
        var oldHash = "pbkdf2-sha256$600000$" + Convert.ToHexStringLower(salt) + "$" +
            Convert.ToHexStringLower(Rfc2898DeriveBytes.Pbkdf2(historical,salt,600000,HashAlgorithmName.SHA256,32));
        var f = new AuthFixture(); f.Existing(true);
        f.Store.Accounts[AuthFixture.Phone]=(f.Store.Accounts[AuthFixture.Phone].Id,oldHash);
        f.Store.LegacyAdmins[AuthFixture.AdminEmail]=oldHash;
        await f.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=historical});
        await f.Accounts.LoginAdmin(new(){["username"]="admin",["password"]=historical});
        check(f.Request.CustomerToken!=null && f.Request.AdminToken!=null,"unchanged historical customer passwords and fixed-username administrator passwords remain valid at login");
        var customer = f.Request.CustomerToken; var refresh=f.Request.RefreshToken;
        await f.Accounts.LogoutAdmin();
        await f.Accounts.GetCustomer();
        check(f.Request.CustomerToken==customer && f.Request.RefreshToken==refresh && f.Request.AdminToken==null,"admin logout preserves both customer credentials");
        await f.Accounts.LoginAdmin(new(){["username"]="admin",["password"]=historical});
        var adminRefresh=f.Request.AdminRefreshToken;
        f.Clock.Advance(901);
        await AuthScenarios.Reject(()=>f.Accounts.GetCustomer(),ErrorKind.Unauthenticated);
        await AuthScenarios.Reject(()=>f.Accounts.GetAdmin(),ErrorKind.Unauthenticated);
        await f.Sessions.Refresh();
        check(f.Request.AdminRefreshToken==adminRefresh,"customer renewal does not rotate administrator credentials");
        var refreshedCustomer=f.Request.CustomerToken;
        await f.Sessions.Refresh(true);
        check(f.Request.CustomerToken==refreshedCustomer && f.Request.AdminRefreshToken!=adminRefresh,"administrator renewal rotates independently");
        var adminSession=f.Store.Access.Values.Single(x=>x.LegacyAdminEmail!=null&&x.RevokedAt==null);
        var forged=new FakeRequest {RefreshToken=f.Request.AdminRefreshToken,AdminRefreshToken=f.Request.RefreshToken};
        forged.SetToken(false,f.Request.AdminToken!,TimeSpan.FromMinutes(1));
        forged.SetToken(true,f.Request.CustomerToken!,TimeSpan.FromMinutes(1));
        var wrong=new SessionService(f.Store,f.Store,f.Store,forged,f.Clock);
        await AuthScenarios.Reject(()=>wrong.Status(),ErrorKind.Unauthenticated);
        await AuthScenarios.Reject(()=>wrong.Status(true),ErrorKind.Unauthenticated);
        await AuthScenarios.Reject(()=>wrong.Refresh(),ErrorKind.Unauthenticated);
        await AuthScenarios.Reject(()=>wrong.Refresh(true),ErrorKind.Unauthenticated);
        check(f.Store.Access[adminSession.Id].RevokedAt==null,"cross-context access and refresh substitution is rejected without revoking the other context");
        var service=new AdministratorService(f.Store,f.Store,f.Sessions,f.Passwords);
        var previousAdminHash=f.Store.LegacyAdmins[AuthFixture.AdminEmail];
        await AuthScenarios.Reject(()=>service.ChangePassword(new(){["currentPassword"]="wrong",["password"]="Admin!6",["confirmation"]="Admin!6"}),ErrorKind.Invalid);
        check(previousAdminHash==f.Store.LegacyAdmins[AuthFixture.AdminEmail],"incorrect current administrator password cannot change credentials");
        await service.ChangePassword(new(){["currentPassword"]=historical,["password"]="Admin!6",["confirmation"]="Admin!6"});
        await f.Accounts.GetCustomer();
        check(f.Request.AdminToken==null && f.Store.Access.Values.Where(x=>x.LegacyAdminEmail!=null).All(x=>x.RevokedAt!=null),"administrator password change revokes all admin families and preserves the customer session");
        await AuthScenarios.Reject(()=>f.Accounts.LoginAdmin(new(){["username"]="admin",["password"]=historical}),ErrorKind.Unauthenticated);
        await f.Accounts.LoginAdmin(new(){["username"]="admin",["password"]="Admin!6"});
        f.Clock.Advance(30*86400);
        await AuthScenarios.Reject(()=>f.Sessions.Refresh(true),ErrorKind.Unauthenticated);
        check(true,"administrator refresh has the same absolute expiry and revocation enforcement");
        var replay=new AuthFixture(); replay.Existing(true);
        replay.Store.LegacyAdmins[AuthFixture.AdminEmail]=replay.Passwords.Hash(AuthFixture.Password);
        await replay.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=AuthFixture.Password});
        await replay.Accounts.LoginAdmin(new(){["username"]="admin",["password"]=AuthFixture.Password});
        var oldAdminRefresh=replay.Request.AdminRefreshToken;
        replay.Clock.Advance(901); await replay.Sessions.Refresh(true);
        var stale=new SessionService(replay.Store,replay.Store,replay.Store,new FakeRequest {AdminRefreshToken=oldAdminRefresh},replay.Clock);
        await AuthScenarios.Reject(()=>stale.Refresh(true),ErrorKind.Conflict);
        replay.Clock.Advance(11);
        await AuthScenarios.Reject(()=>stale.Refresh(true),ErrorKind.Unauthenticated);
        await replay.Sessions.Refresh();
        await replay.Accounts.GetCustomer();
        check(replay.Store.Access.Values.Where(x=>x.LegacyAdminEmail!=null).All(x=>x.RevokedAt!=null),"administrator refresh replay revokes only the admin family after the concurrency grace window");
        var six=new AuthFixture();
        var registration=await six.Send();
        var registrationBody=AuthFixture.RegistrationBody(AuthFixture.Data(await six.Verify(registration)));
        registrationBody["password"]="A!1234";registrationBody["confirmation"]="A!1234";
        await six.Otp.CompleteRegistration(registrationBody);
        await six.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]="A!1234"});
        six.Clock.Advance(61);
        var grant=AuthFixture.Data(await six.Verify(await six.Send("reset"),reset:true));
        foreach(var invalid in new[]{"123456","chap@123","CHAP123","Ab123"})
            await AuthScenarios.Reject(()=>six.Otp.ResetPassword(AuthFixture.ResetBody(grant,invalid)),ErrorKind.Invalid);
        check(six.Store.Grants.Values.All(x=>!x.Consumed),"invalid recovery passwords preserve the unconsumed grant");
        await six.Otp.ResetPassword(AuthFixture.ResetBody(grant,"Giti!6"));
        await six.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]="Giti!6"});
        check(true,"registration and OTP password recovery accept new compliant six-character passwords");
        try { await six.Accounts.SetupAdmin("test@example.test","admin","123456"); throw new Exception("Invalid setup password accepted."); }
        catch(ChapGiti.Domain.Common.RuleViolation) { check(true,"administrator setup enforces the shared policy before persistence"); }
        var fixedIdentity=new AuthFixture();
        fixedIdentity.Store.LegacyAdmins[AuthFixture.AdminEmail]=fixedIdentity.Passwords.Hash(AuthFixture.Password);
        var unchangedHash=fixedIdentity.Store.LegacyAdmins[AuthFixture.AdminEmail];
        await AuthScenarios.Reject(async () => { await fixedIdentity.Accounts.SetupAdmin(AuthFixture.AdminEmail,"another","Admin!6"); return UseCaseResult.Json(new { }); },ErrorKind.Invalid);
        await AuthScenarios.Reject(async () => { await fixedIdentity.Accounts.SetupAdmin("other@example.test","admin","Admin!6"); return UseCaseResult.Json(new { }); },ErrorKind.Conflict);
        check(fixedIdentity.Store.AdminUsernames["admin"]==AuthFixture.AdminEmail && fixedIdentity.Store.LegacyAdmins[AuthFixture.AdminEmail]==unchangedHash,
            "setup cannot rename or transfer the existing admin identity or change its password on failure");
        fixedIdentity.Store.AdminUsernames.Clear();
        await AuthScenarios.Reject(()=>fixedIdentity.Accounts.LoginAdmin(new(){["username"]="admin",["password"]=AuthFixture.Password}),ErrorKind.Unauthenticated);
        check(fixedIdentity.Store.AdminUsernames.Count==0 && fixedIdentity.Store.Access.Count==0 && fixedIdentity.Store.LegacyAdmins.Count==1,
            "a missing admin mapping fails closed without selecting another account or writing a replacement mapping");
        await Api(check);
    }

    private static async Task Api(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        using var client=new HttpClient(new HttpClientHandler {UseCookies=false}) {BaseAddress=new Uri(host.Origin)};
        var cookies=new Dictionary<string,string>();
        var originalPasswordHash=host.Fixture.Store.LegacyAdmins[AuthFixture.AdminEmail];
        async Task<JsonNode> Request(string method,string path,object? body=null,int expected=200,Dictionary<string,string>? jar=null)
        {
            jar ??= cookies;
            using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+path);
            request.Headers.Add("Origin",host.Origin);
            if(jar.Count>0)request.Headers.Add("Cookie",string.Join("; ",jar.Select(p=>p.Key+"="+p.Value)));
            if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);
            var text=await response.Content.ReadAsStringAsync();
            if((int)response.StatusCode!=expected)throw new InvalidOperationException(path+" expected "+expected+" got "+(int)response.StatusCode+": "+text);
            if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var value in values)
            {
                var pair=value.Split(';')[0].Split('=',2);
                if(pair[1]=="")jar.Remove(pair[0]);else jar[pair[0]]=pair[1];
            }
            check(true,"independent auth HTTP "+method+" "+path+" -> "+expected);
            return JsonNode.Parse(text)!;
        }
        await Request("POST","customer/login",new {phone=AuthFixture.Phone,password=AuthFixture.Password});
        var customerCookies=cookies.ToDictionary();
        await Request("GET","auth/me",expected:401);
        await Request("GET","admin/profile",expected:401);
        await Request("POST","auth/login",new {email=AuthFixture.AdminEmail,password=AuthFixture.Password},400);
        await Request("POST","auth/login",new {username="Admin",password=AuthFixture.Password},401);
        await Request("POST","auth/login",new {username="other",password=AuthFixture.Password},401);
        await Request("POST","auth/login",new {username="admin",password="incorrect"},401);
        await Request("POST","auth/login",new {username="admin",password=AuthFixture.Password});
        check(cookies.Count==4 && customerCookies.All(p=>cookies[p.Key]==p.Value),"HTTP administrator login creates two different cookies without changing customer cookies");
        var adminOnly=cookies.Where(p=>p.Key.StartsWith("giti_admin_")).ToDictionary();
        await Request("GET","customer/me",expected:401,jar:adminOnly);
        await Request("GET","auth/me",expected:401,jar:customerCookies);
        await Request("POST","customer/refresh",expected:401,jar:new(){["giti_customer_refresh"]=cookies["giti_admin_refresh"]});
        await Request("POST","auth/refresh",expected:401,jar:new(){["giti_admin_refresh"]=cookies["giti_customer_refresh"]});
        await Request("GET","auth/me",expected:401,jar:new(){["giti_access"]=cookies["giti_admin_access"],["giti_refresh"]=cookies["giti_admin_refresh"]});
        check(true,"retired shared cookies cannot restore either new authentication scheme");
        await Request("PUT","admin/profile/email",new {email="not-an-email"},400);
        host.Fixture.Store.LegacyAdmins["second@example.test"]=host.Fixture.Store.LegacyAdmins[AuthFixture.AdminEmail];
        var countBefore=host.Fixture.Store.LegacyAdmins.Count;
        await Request("POST","admin/administrators",new {email="new@example.test",password="Admin!6",confirmation="Admin!6"},409);
        check(host.Fixture.Store.LegacyAdmins.Count==countBefore,"fixed-username administration cannot create inaccessible email-based administrators");
        await Request("PUT","admin/profile/email",new {email="second@example.test"},409);
        var secondHash=host.Fixture.Store.LegacyAdmins["second@example.test"];
        await Request("PUT","admin/profile/email",new {email="hijacked@example.test",id="second@example.test"},400);
        await Request("PUT","admin/profile/email",new {email="unchanged@example.test",username="different"},400);
        await Request("PUT","admin/profile/password",new {currentPassword=AuthFixture.Password,password="Admin!6",confirmation="Admin!6",email="second@example.test"},400);
        await Request("PUT","admin/profile/password",new {currentPassword=AuthFixture.Password,password="Admin!6",confirmation="Admin!6",username="another"},400);
        await Request("PUT","admin/profile/second@example.test",new {email="hijacked@example.test"},404);
        check(host.Fixture.Store.LegacyAdmins["second@example.test"]==secondHash,"self-service profile cannot target another administrator");
        await Request("PUT","admin/profile/email",new {email=" OWN@example.test "});
        var profile=await Request("GET","admin/profile");
        check(profile["email"]!.ToString()=="own@example.test" && profile["username"]!.ToString()=="admin" && host.Fixture.Store.AdminUsernames["admin"]==AuthFixture.AdminEmail && host.Fixture.Store.LegacyAdmins[AuthFixture.AdminEmail]==originalPasswordHash,"email updates preserve the immutable admin username, original account and password");
        host.Fixture.Store.AdminEmailSupported=false;
        await Request("PUT","admin/profile/email",new {email="unavailable@example.test"},503);
        await Request("GET","auth/me");
        await Request("GET","customer/me");
        check(host.Fixture.Store.AdminEmails[AuthFixture.AdminEmail]=="own@example.test","unavailable email editing leaves accounts and authentication intact");
        host.Fixture.Store.AdminEmailSupported=true;
        await Request("POST","auth/logout");
        await Request("GET","customer/me");
        await Request("POST","auth/login",new {username=AuthFixture.AdminEmail,password=AuthFixture.Password},401);
        await Request("POST","auth/login",new {username="own@example.test",password=AuthFixture.Password},401);
        await Request("POST","auth/login",new {username="admin",password=AuthFixture.Password});
        var policyChallenge=await Request("POST","customer/otp/request",new {phone="09155555555",purpose="register"});
        var policyGrant=await Request("POST","customer/otp/verify",new {challengeId=policyChallenge!["challengeId"]!.ToString(),code="0123"});
        foreach(var invalid in new[]{"123456","chap@123","CHAP123","Ab123"})
        {
            await Request("PUT","admin/profile/password",new {currentPassword=AuthFixture.Password,password=invalid,confirmation=invalid},400);
            await Request("POST","admin/users",new {firstName="Test",lastName="Policy",phone="09166666666",password=invalid,confirmation=invalid},400);
            await Request("POST","customer/registration/complete",new {registrationToken=policyGrant!["registrationToken"]!.ToString(),firstName="Test",lastName="Policy",password=invalid,confirmation=invalid},400);
        }
        await Request("PUT","admin/profile/password",new {currentPassword="incorrect",password="Test#2026",confirmation="Test#2026"},400);
        await Request("PUT","admin/profile/password",new {currentPassword=AuthFixture.Password,password="Test#2026",confirmation="different"},400);
        await Request("PUT","admin/profile/password",new {currentPassword=AuthFixture.Password,password="Test#2026",confirmation="Test#2026"});
        await Request("GET","auth/me",expected:401);
        await Request("GET","customer/me");
        await Request("POST","auth/refresh",expected:401,jar:adminOnly);
        await Request("POST","auth/login",new {username="admin",password="Test#2026"});
        var adminCredential=cookies["giti_admin_access"];
        await Request("POST","customer/logout");
        await Request("GET","auth/me");
        check(cookies["giti_admin_access"]==adminCredential,"customer logout leaves the administrator cookie and backend authorization intact");
        var directory=await Request("GET","admin/administrators");
        check(!directory.ToJsonString().Contains("password_hash") && host.Fixture.Sender.Sends==1,"administrator directory exposes no credential hashes; only the explicit registration policy check invokes fake SMS");
    }
}
