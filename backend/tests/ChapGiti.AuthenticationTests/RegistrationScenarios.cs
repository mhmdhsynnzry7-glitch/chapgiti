using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class RegistrationScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        foreach (var phone in new[] { AuthFixture.Phone, "۰۹۰۰۰۰۰۰۸۲۶", "٠٩٠٠٠٠٠٠٨٢٦" })
        {
            var f = new AuthFixture();
            var challenge = await f.Send(phone: phone);
            check(f.Store.Accounts.Count == 0 && f.Store.Profiles.Count == 0 && f.Store.Pending.Count == 0,
                "registration starts with only a normalized phone, not a profile/password/account");
            await AuthScenarios.Reject(() => f.Verify(challenge, "9999"), ErrorKind.Invalid);
            check(f.Store.Challenges.Values.All(c => c.DeliveryStatus != "grant"), "wrong OTP cannot issue a registration grant");
            var grant = AuthFixture.Data(await f.Verify(challenge, "٠١٢٣"));
            check(grant.Count == 4 && grant["phone"]!.ToString() == AuthFixture.Phone &&
                grant["expiresAt"]!.GetValue<long>() - f.Clock.GetUtcNow().ToUnixTimeMilliseconds() == 300000 &&
                f.Store.Profiles.Count == 0 && f.Store.Accounts.Count == 0,
                "OTP verification returns a five-minute phone-bound grant without a customer profile");
            check(!f.Store.Challenges.ContainsKey(grant["registrationToken"]!.ToString()) &&
                f.Store.Challenges.Values.Single(c => c.DeliveryStatus == "grant").CodeHash == null,
                "registration stores only a token digest, never the raw grant, OTP or profile payload");
            check(await f.Store.CountRequests(AuthFixture.Phone, 0) == 1 &&
                (await f.Store.Latest(AuthFixture.Phone))!.Id == challenge["challengeId"]!.ToString(),
                "registration grants do not consume SMS quota or alter resend cooldown lookup");
            await f.Complete(grant);
            var account = f.Store.Accounts[AuthFixture.Phone];
            check(f.Store.Profiles[account.Id].Verified && f.Store.Profiles[account.Id].First == "Test" &&
                f.Passwords.Verify(AuthFixture.Password, account.Hash) && f.Request.CustomerToken == null,
                "profile completion consumes verified ownership and stores a salted password without logging in");
            await AuthScenarios.Reject(() => f.Complete(grant), ErrorKind.Invalid);
            await f.Accounts.LoginCustomer(new() { ["phone"] = phone, ["password"] = AuthFixture.Password });
            check(f.Store.Accounts.Count == 1 && f.Request.CustomerToken != null,
                "the completed account signs in with normalized mobile/password and cannot reuse its grant");
        }
        {
            var f = new AuthFixture();
            var challenge = await f.Send();
            await AuthScenarios.Reject(() => f.Complete(new() { ["registrationToken"] = challenge["challengeId"]!.ToString() }), ErrorKind.Invalid);
            await AuthScenarios.Reject(() => f.Complete(new() { ["registrationToken"] = new string('f', 64) }), ErrorKind.Invalid);
            f.Clock.Advance(120);
            await AuthScenarios.Reject(() => f.Verify(challenge), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 0 && f.Store.Profiles.Count == 0 && f.Store.Challenges.Values.All(c => c.DeliveryStatus != "grant"),
                "challenge IDs, forged grants and expired OTP cannot authorize profile submission");
        }
        foreach (var age in new[] { 299, 300 })
        {
            var f = new AuthFixture();
            var grant = AuthFixture.Data(await f.Verify(await f.Send()));
            f.Clock.Advance(age);
            if (age == 300) await AuthScenarios.Reject(() => f.Complete(grant), ErrorKind.Invalid);
            else await f.Complete(grant);
            check(f.Store.Accounts.Count == (age == 300 ? 0 : 1), "registration grant expires at the exact five-minute boundary");
        }
        {
            var f = new AuthFixture();
            var grant = AuthFixture.Data(await f.Verify(await f.Send()));
            var id = f.Existing(true);
            var hash = f.Store.Accounts[AuthFixture.Phone].Hash;
            await AuthScenarios.Reject(() => f.Complete(grant), ErrorKind.Conflict);
            check(f.Store.Accounts.Count == 1 && f.Store.Accounts[AuthFixture.Phone].Hash == hash && f.Store.Profiles[id].First == "Test",
                "an intervening registration cannot be overwritten by an older verified grant");
        }
        {
            var f = new AuthFixture();
            var grant = AuthFixture.Data(await f.Verify(await f.Send()));
            async Task<bool> Complete()
            {
                try { await f.Complete(grant); return true; }
                catch (UseCaseException) { return false; }
            }
            var results = await Task.WhenAll(Complete(), Complete());
            check(results.Count(x => x) == 1 && f.Store.Accounts.Count == 1 && f.Store.Profiles.Count == 1,
                "concurrent profile completions consume a registration grant exactly once");
        }
        {
            var f = new AuthFixture();
            var grant = AuthFixture.Data(await f.Verify(await f.Send()));
            f.Store.FailNextAccountCreation = true;
            try { await f.Complete(grant); throw new Exception("Expected isolated write failure."); }
            catch (InvalidOperationException) { }
            check(f.Store.Accounts.Count == 0 && f.Store.Profiles.Count == 0 &&
                !f.Store.Challenges.Values.Single(c => c.DeliveryStatus == "grant").Consumed,
                "failed account persistence rolls back the profile and registration grant consumption");
            await f.Complete(grant);
            check(f.Store.Accounts.Count == 1, "the unconsumed grant can complete after a rolled-back persistence failure");
        }
        {
            var f = new AuthFixture();
            var grant = AuthFixture.Data(await f.Verify(await f.Send()));
            f.Clock.Advance(60);
            await f.Send();
            await AuthScenarios.Reject(() => f.Complete(grant), ErrorKind.Invalid);
            check(f.Sender.Sends == 2 && await f.Store.CountRequests(AuthFixture.Phone, 0) == 2,
                "a replacement OTP invalidates prior grants without counting grants as SMS requests");
        }
        foreach (var enabled in new[] { false, true })
        {
            var f = new AuthFixture(verificationEnabled: enabled);
            var grant = AuthFixture.Data(await f.Verify(await f.Send()));
            await AuthScenarios.Reject(() => f.OtpInMode(!enabled).CompleteRegistration(AuthFixture.RegistrationBody(grant)), ErrorKind.Invalid);
            await f.Complete(grant);
            check(f.Store.Accounts.Count == 1, "registration grants cannot cross real/development verification modes");
        }
        {
            var f = new AuthFixture();
            f.Existing(true);
            var reset = AuthFixture.Data(await f.Verify(await f.Send("reset"), reset: true));
            await AuthScenarios.Reject(() => f.Complete(new() { ["registrationToken"] = reset["resetToken"]!.ToString() }), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 1 && f.Store.Grants.Values.All(g => !g.Consumed),
                "password-recovery grants cannot create registrations or be consumed for another purpose");
        }
        await ApiChecks(check);
    }

    private static async Task ApiChecks(Action<bool, string> check)
    {
        await using var host = await TestApiHost.Start();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(host.Origin) };
        async Task<JsonObject> Post(string path, object body, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            if (response.StatusCode != expected) throw new InvalidOperationException(path + " returned unexpected status " + (int)response.StatusCode);
            check(true, "registration HTTP " + path + " -> " + (int)expected);
            if (path != "login") check(!response.Headers.Contains("Set-Cookie"), "registration steps never create customer or administrator sessions");
            return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        }
        const string phone = "09177777777";
        await Post("otp/request", new { phone, purpose = "register", firstName = "Premature" }, HttpStatusCode.BadRequest);
        await Post("registration/complete", new { firstName = "Unverified", lastName = "Customer", password = AuthFixture.Password, confirmation = AuthFixture.Password }, HttpStatusCode.BadRequest);
        var challenge = await Post("otp/request", new { phone = "۰۹۱۷۷۷۷۷۷۷۷", purpose = "register" });
        await Post("otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "9999" }, HttpStatusCode.BadRequest);
        var grant = await Post("otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "٠١٢٣" });
        check(!host.Fixture.Store.Accounts.ContainsKey(phone) && host.Fixture.Store.Pending.Count == 0,
            "HTTP OTP verification authorizes the next step without profile collection or persistence");
        var body = AuthFixture.RegistrationBody(grant);
        body["phone"] = "09111111111";
        await Post("registration/complete", body, HttpStatusCode.BadRequest);
        body.Remove("phone"); body["password"] = "weak";
        await Post("registration/complete", body, HttpStatusCode.BadRequest);
        body["password"] = AuthFixture.Password; body["confirmation"] = "Different.Password@1";
        await Post("registration/complete", body, HttpStatusCode.BadRequest);
        body["confirmation"] = AuthFixture.Password;
        await Post("registration/complete", body);
        await Post("registration/complete", body, HttpStatusCode.BadRequest);
        await Post("otp/request", new { phone, purpose = "register" }, HttpStatusCode.Conflict);
        await Post("login", new { phone = "٠٩١٧٧٧٧٧٧٧٧", password = AuthFixture.Password });
        var expiring = await Post("otp/request", new { phone = "09166666666", purpose = "register" });
        var expires = await Post("otp/verify", new { challengeId = expiring["challengeId"]!.ToString(), code = "0123" });
        host.Fixture.Clock.Advance(300);
        var error = await Post("registration/complete", AuthFixture.RegistrationBody(expires), HttpStatusCode.BadRequest);
        check(error["code"]?.ToString() == "REGISTRATION_GRANT_INVALID" && !host.Fixture.Store.Accounts.ContainsKey("09166666666"),
            "expired HTTP registration grants return a structured error without creating an account");
        check(host.Fixture.Sender.Sends == 2, "HTTP registration uses only the mocked SMS provider, once per challenge");
    }
}
