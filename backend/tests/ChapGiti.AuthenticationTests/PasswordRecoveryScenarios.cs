using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class PasswordRecoveryScenarios
{
    private static async Task Error(Func<Task<UseCaseResult>> action, string code, Action<bool, string> check)
    {
        try { await action(); throw new InvalidOperationException("Expected recovery rejection: " + code); }
        catch (UseCaseException error) { check(error.Code == code, "recovery returns structured " + code); }
    }

    public static async Task Run(Action<bool, string> check)
    {
        foreach (var phone in new[] { AuthFixture.Phone, "۰۹۰۰۰۰۰۰۸۲۶", "٠٩٠٠٠٠٠٠٨٢٦" })
        {
            var f = new AuthFixture();
            await Error(() => f.Otp.Request(new() { ["phone"] = phone, ["purpose"] = "reset" }), "CUSTOMER_ACCOUNT_NOT_FOUND", check);
            check(f.Sender.Sends == 0 && f.Store.Challenges.Count == 0 && f.Store.Accounts.Count == 0 && f.Store.Profiles.Count == 0,
                "unknown normalized recovery mobile creates no OTP, customer, profile or provider invocation");
            f.Existing(true);
            var challenge = await f.Send("reset", phone);
            check(f.Sender.Sends == 1 && f.Store.Challenges[challenge["challengeId"]!.ToString()].CustomerId == f.Store.Accounts[AuthFixture.Phone].Id,
                "eligible normalized customer recovery binds the issued challenge to the actual account");
            await Error(() => f.Verify(challenge, "9999", reset: true), "OTP_INCORRECT", check);
            var grant = AuthFixture.Data(await f.Verify(challenge, "٠١٢٣", reset: true));
            await Error(() => f.Verify(challenge, reset: true), "OTP_CONSUMED", check);
            await f.Otp.ResetPassword(AuthFixture.ResetBody(grant));
            await f.Accounts.LoginCustomer(new() { ["phone"] = phone, ["password"] = AuthFixture.NewPassword });
            check(f.Request.CustomerToken != null, "eligible customers complete recovery and password login using isolated test credentials");
        }
        {
            var f = new AuthFixture(); var id = f.Existing(false);
            await Error(async () => { await f.Send("reset"); return new(); }, "CUSTOMER_PHONE_UNVERIFIED", check);
            check(!f.Store.Profiles[id].Verified && f.Sender.Sends == 0 && f.Store.Challenges.Count == 0,
                "legacy unverified accounts remain ineligible without a silent verification or SMS");
            f.Store.Profiles[id] = f.Store.Profiles[id] with { Verified = true, First = "" };
            await Error(async () => { await f.Send("reset"); return new(); }, "CUSTOMER_RECOVERY_UNAVAILABLE", check);
            check(f.Sender.Sends == 0 && f.Store.Challenges.Count == 0, "incomplete customer profiles cannot begin password recovery");
        }
        {
            var f = new AuthFixture(); var signup = await f.Send();
            var id = signup["challengeId"]!.ToString();
            f.Store.Pending[id] = new(id, "Old", "Pending", "historical-test-hash");
            await Error(async () => { await f.Send("reset"); return new(); }, "CUSTOMER_ACCOUNT_NOT_FOUND", check);
            check(f.Sender.Sends == 1 && f.Store.Challenges.Count == 1 && f.Store.Pending.Count == 1,
                "pending registrations are not accounts and recovery leaves their existing isolated state unchanged");
            await f.Verify(signup);
            var count = f.Store.Challenges.Count;
            await Error(async () => { await f.Send("reset"); return new(); }, "CUSTOMER_ACCOUNT_NOT_FOUND", check);
            check(f.Sender.Sends == 1 && f.Store.Challenges.Count == count, "a registration grant alone is not recovery eligibility");
        }
        {
            var f = new AuthFixture();
            f.Sender.Available = false;
            await Error(async () => { await f.Send("reset"); return new(); }, "CUSTOMER_ACCOUNT_NOT_FOUND", check);
            f.Existing(true);
            await Error(async () => { await f.Send("reset"); return new(); }, "SMS_DELIVERY_FAILED", check);
            check(f.Sender.Sends == 0 && f.Store.Challenges.Count == 0, "eligibility is checked before provider availability and no unavailable challenge is issued");
            f.Sender.Available = true; f.Sender.Fail = true;
            await Error(async () => { await f.Send("reset"); return new(); }, "SMS_DELIVERY_FAILED", check);
            var failed = f.Store.Challenges.Values.Single();
            await Error(() => f.Otp.VerifyReset(new() { ["challengeId"] = failed.Id, ["code"] = "0123" }), "SMS_DELIVERY_FAILED", check);
            check(failed.Consumed && failed.CodeHash == null && f.Store.Grants.Count == 0, "provider failure cannot leave a usable challenge or recovery grant");
        }
        {
            var f = new AuthFixture(); f.Existing(true); var c = await f.Send("reset");
            f.Clock.Advance(120);
            await Error(() => f.Verify(c, reset: true), "OTP_EXPIRED", check);
            await Error(() => f.Otp.VerifyReset(new() { ["challengeId"] = new string('f', 64), ["code"] = "0123" }), "OTP_CHALLENGE_MISSING", check);
            await Error(() => f.Otp.VerifyReset(new() { ["challengeId"] = "stale-id", ["code"] = "0123" }), "OTP_CHALLENGE_MISSING", check);
            await Error(() => f.Otp.VerifyReset(new() { ["code"] = "0123" }), "OTP_CHALLENGE_MISSING", check);
            check(f.Store.Grants.Count == 0, "missing, malformed and expired challenges cannot advance to password reset");
        }
        {
            var f = new AuthFixture(); f.Existing(true); var c = await f.Send("reset");
            for (var n = 0; n < 4; n++) await Error(() => f.Verify(c, "9999", reset: true), "OTP_INCORRECT", check);
            await Error(() => f.Verify(c, "9999", reset: true), "OTP_ATTEMPT_LIMIT", check);
            await Error(() => f.Verify(c, reset: true), "OTP_ATTEMPT_LIMIT", check);
            check(f.Store.Challenges[c["challengeId"]!.ToString()].Attempts == 5 && f.Store.Grants.Count == 0,
                "attempt-limit errors are distinct from consumed or expired codes and the limit commits");
        }
        foreach (var stale in new[] { "missing", "unverified", "incomplete", "replaced" })
        {
            var f = new AuthFixture(); var customer = f.Existing(true); var c = await f.Send("reset");
            if (stale == "missing") f.Store.Accounts.Remove(AuthFixture.Phone);
            if (stale == "unverified") f.Store.Profiles[customer] = f.Store.Profiles[customer] with { Verified = false };
            if (stale == "incomplete") f.Store.Profiles[customer] = f.Store.Profiles[customer] with { Last = "" };
            if (stale == "replaced") f.Existing(true);
            var expected = stale switch { "missing" => "CUSTOMER_ACCOUNT_NOT_FOUND", "unverified" => "CUSTOMER_PHONE_UNVERIFIED",
                "incomplete" => "CUSTOMER_RECOVERY_UNAVAILABLE", _ => "OTP_CHALLENGE_INVALID" };
            await Error(() => f.Verify(c, "9999", reset: true), expected, check);
            check(f.Store.Challenges[c["challengeId"]!.ToString()].Consumed && f.Store.Grants.Count == 0,
                "stale recovery eligibility is rechecked before code verification and invalidation commits: " + stale);
        }
        {
            var f = new AuthFixture(); var id = new string('e', 64); var now = f.Clock.GetUtcNow().ToUnixTimeMilliseconds();
            f.Store.Challenges[id] = new(id, AuthFixture.Phone, "reset", null, "old-code-digest", now, now + 120000, 0, false, "sent");
            await Error(() => f.Otp.VerifyReset(new() { ["challengeId"] = id, ["code"] = "9999" }), "CUSTOMER_ACCOUNT_NOT_FOUND", check);
            check(f.Store.Challenges[id].Consumed && f.Store.Grants.Count == 0 && f.Store.Accounts.Count == 0 && f.Sender.Sends == 0,
                "an old unknown-account challenge is revoked without accepting it as evidence of an account");
        }
        await ApiChecks(check);
    }

    private static async Task ApiChecks(Action<bool, string> check)
    {
        await using var host = await TestApiHost.Start();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(host.Origin) };
        async Task<JsonObject> Post(string path, object body, int expected = 200, string? code = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            var result = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
            check((int)response.StatusCode == expected && (code == null || result["code"]?.ToString() == code),
                "recovery HTTP " + path + " -> " + expected + (code == null ? "" : " " + code));
            return result;
        }
        foreach (var phone in new[] { "09177777777", "۰۹۱۷۷۷۷۷۷۷۷", "٠٩١٧٧٧٧٧٧٧٧" })
        {
            var result = await Post("otp/request", new { phone, purpose = "reset" }, 404, "CUSTOMER_ACCOUNT_NOT_FOUND");
            check(result["error"]?.ToString() == "حساب کاربری با این شماره موبایل پیدا نشد. برای ایجاد حساب جدید، لطفاً ثبت‌نام کنید." &&
                host.Fixture.Sender.Sends == 0 && host.Fixture.Store.Challenges.Count == 0 && host.Fixture.Store.Accounts.Count == 1,
                "unknown HTTP recovery returns exact Persian guidance without SMS, challenge or customer creation");
        }
        await Post("otp/request", new { phone = "invalid", purpose = "reset" }, 400);
        var c = await Post("otp/request", new { phone = "۰۹۰۰۰۰۰۰۸۲۶", purpose = "reset" });
        await Post("password/verify", new { challengeId = c["challengeId"]!.ToString(), code = "9999" }, 400, "OTP_INCORRECT");
        var grant = await Post("password/verify", new { challengeId = c["challengeId"]!.ToString(), code = "٠١٢٣" });
        await Post("password/verify", new { challengeId = c["challengeId"]!.ToString(), code = "0123" }, 400, "OTP_CONSUMED");
        await Post("password/reset", AuthFixture.ResetBody(grant));
        await Post("login", new { phone = AuthFixture.Phone, password = AuthFixture.NewPassword });
        await Post("password/verify", new { challengeId = "old-client-state", code = "0123" }, 400, "OTP_CHALLENGE_MISSING");
        var customer = host.Fixture.Store.Accounts[AuthFixture.Phone].Id;
        host.Fixture.Store.Profiles[customer] = host.Fixture.Store.Profiles[customer] with { Verified = false };
        await Post("otp/request", new { phone = AuthFixture.Phone, purpose = "reset" }, 403, "CUSTOMER_PHONE_UNVERIFIED");
        check(host.Fixture.Sender.Sends == 1 && host.Fixture.Store.Grants.Values.All(g => g.Consumed),
            "HTTP recovery eligibility and replay failures do not send additional SMS or create reset grants");
    }
}
