using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ChapGiti.AuthenticationTests;

internal static class OtpProviderApiScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        // Real MVC, use cases, HMAC protector and MelliPayamak adapter; only
        // persistence, time and the adapter's outbound HTTP transport are faked.
        var handler = new ProviderHandler { Response = """{"code":"4068","status":"provider diagnostic text"}""" };
        await using var host = await TestApiHost.Start(otpProviderHandler: handler);
        using var client = new HttpClient { BaseAddress = new Uri(host.Origin) };
        async Task<JsonObject> Post(string path, object body, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != expected) throw new InvalidOperationException(path + " returned " + response.StatusCode + ": " + text);
            check(true, "real-mode HTTP " + path + " -> " + (int)expected + " with mocked provider transport");
            if (expected == HttpStatusCode.TooManyRequests)
                check(response.Headers.RetryAfter?.Delta?.TotalSeconds > 0, "OTP cooldown API includes Retry-After");
            return JsonNode.Parse(text)!.AsObject();
        }
        object Registration(string phone) => new { phone, purpose = "register" };
        object Code(JsonObject challenge, string code) => new { challengeId = challenge["challengeId"]!.ToString(), code };

        const string newPhone = "09177777777";
        var challenge = await Post("otp/request", Registration(newPhone));
        check(handler.Requests == 1 && JsonNode.Parse(handler.Body!)!["to"]!.ToString() == newPhone &&
            host.Fixture.Store.Accounts.Count == 1 && challenge["codeLength"]!.GetValue<int>() == 4 &&
            challenge.Count == 6 && new[] { "ok", "challengeId", "expiresAt", "resendAfterSeconds", "codeLength", "message" }.All(challenge.ContainsKey) &&
            !challenge["message"]!.ToString().Contains("4068"),
            "registration Controller invokes the actual SMS adapter once without exposing the OTP or prematurely creating an account");
        var protector = host.App.Services.GetRequiredService<IOtpCodeProtector>();
        var registrationId = challenge["challengeId"]!.ToString();
        check(host.Fixture.Store.Challenges[registrationId].CodeHash == protector.Hash(registrationId, newPhone, "register", "4068"),
            "registration stores the exact provider-issued code as a phone-, purpose- and challenge-bound HMAC without generating another OTP");
        await Post("otp/verify", Code(challenge, "9999"), HttpStatusCode.BadRequest);
        await Post("password/verify", Code(challenge, "4068"), HttpStatusCode.BadRequest);
        var registration = await Post("otp/verify", Code(challenge, "۴۰۶۸"));
        check(!host.Fixture.Store.Accounts.ContainsKey(newPhone), "valid provider OTP issues only a grant before collecting profile/password");
        await Post("registration/complete", AuthFixture.RegistrationBody(registration));
        var created = host.Fixture.Store.Accounts[newPhone];
        check(host.Fixture.Store.Profiles[created.Id].Verified && host.Fixture.Store.Access.Count == 0,
            "registration completes only with the purpose-bound provider code and creates no login session");
        await Post("otp/verify", Code(challenge, "4068"), HttpStatusCode.BadRequest);
        await Post("login", new { phone = newPhone, password = "Wrong.Password@1" }, HttpStatusCode.Unauthorized);
        await Post("login", new { phone = newPhone, password = AuthFixture.Password });

        handler.Response = """{"code":"6170","status":"provider diagnostic text"}""";
        var recovery = await Post("otp/request", new { phone = AuthFixture.Phone, purpose = "reset" });
        check(handler.Requests == 2 && host.Fixture.Store.Challenges[recovery["challengeId"]!.ToString()].Purpose == "reset",
            "password recovery invokes the adapter with a separate reset challenge");
        var recoveryId = recovery["challengeId"]!.ToString();
        check(host.Fixture.Store.Challenges[recoveryId].CodeHash == protector.Hash(recoveryId, AuthFixture.Phone, "reset", "6170"),
            "password recovery stores the exact provider-issued code with reset-purpose binding");
        await Post("password/verify", Code(recovery, "4068"), HttpStatusCode.BadRequest);
        var grant = await Post("password/verify", Code(recovery, "٦١٧٠"));
        await Post("password/verify", Code(recovery, "6170"), HttpStatusCode.BadRequest);
        await Post("password/reset", new { resetToken = grant["resetToken"]!.ToString(), password = AuthFixture.NewPassword,
            confirmation = AuthFixture.NewPassword });
        check(host.Fixture.Passwords.Verify(AuthFixture.NewPassword, host.Fixture.Store.Accounts[AuthFixture.Phone].Hash),
            "the complete recovery API changes only the in-memory test account after valid OTP and recovery grant");

        var expires = await Post("otp/request", Registration("09166666666"));
        host.Fixture.Clock.Advance(120);
        await Post("otp/verify", Code(expires, "6170"), HttpStatusCode.BadRequest);
        check(!host.Fixture.Store.Accounts.ContainsKey("09166666666"), "an expired real-mode OTP cannot create an account via HTTP");

        var attempts = await Post("otp/request", Registration("09155555555"));
        for (var n = 0; n < 5; n++)
            await Post("otp/verify", Code(attempts, "9999"), HttpStatusCode.BadRequest);
        await Post("otp/verify", Code(attempts, "6170"), HttpStatusCode.BadRequest);
        check(host.Fixture.Store.Challenges[attempts["challengeId"]!.ToString()].Attempts == 5,
            "five invalid codes exhaust the challenge through the verification endpoint");

        var cooldownPhone = "09144444444";
        var cooldownCustomer = Guid.NewGuid().ToString();
        host.Fixture.Store.Profiles[cooldownCustomer] = new(cooldownCustomer, cooldownPhone, "Cooldown", "Customer", true);
        host.Fixture.Store.Accounts[cooldownPhone] = (cooldownCustomer, host.Fixture.Store.Accounts[AuthFixture.Phone].Hash);
        await Post("otp/request", new { phone = cooldownPhone, purpose = "reset" });
        var sent = handler.Requests;
        await Post("otp/request", new { phone = cooldownPhone, purpose = "reset" }, HttpStatusCode.TooManyRequests);
        host.Fixture.Clock.Advance(59);
        await Post("otp/request", new { phone = cooldownPhone, purpose = "reset" }, HttpStatusCode.TooManyRequests);
        check(handler.Requests == sent, "rate-limited API resends do not invoke the provider");
        host.Fixture.Clock.Advance(1);
        await Post("otp/request", new { phone = cooldownPhone, purpose = "reset" });
        check(handler.Requests == sent + 1, "the provider is invoked again after the resend cooldown");

        await ProviderFailures(check);
    }

    private static async Task ProviderFailures(Action<bool, string> check)
    {
        var handler = new ProviderHandler { Response = "sensitive-provider-body" };
        await using var host = await TestApiHost.Start(otpProviderHandler: handler);
        using var client = new HttpClient { BaseAddress = new Uri(host.Origin) };
        var number = 0;
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
        {
            handler.Status = status;
            var phone = "0913333333" + number++;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/otp/request") {
                Content = JsonContent.Create(new { phone, purpose = "register" }) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            check(response.StatusCode == HttpStatusCode.ServiceUnavailable && !text.Contains("sensitive-provider-body") &&
                !text.Contains("isolated-api-test-key"), "provider HTTP " + (int)status + " becomes a sanitized API 503");
            check(handler.Requests == number && host.Fixture.Store.Challenges.Values.All(c => c.Consumed && c.CodeHash == null) &&
                !host.Fixture.Store.Accounts.ContainsKey(phone), "provider errors cannot leave a valid code, create an account or trigger a retry");
        }
        handler.Status = HttpStatusCode.OK;
        foreach (var purpose in new[] { "register", "reset" })
        {
            handler.Response = """{"code":null,"status":"sensitive-provider-body"}""";
            var phone = purpose == "reset" ? AuthFixture.Phone : "09122222222";
            var before = handler.Requests;
            var accountCount = host.Fixture.Store.Accounts.Count;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/otp/request") {
                Content = JsonContent.Create(new { phone, purpose }) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            check(response.StatusCode == HttpStatusCode.ServiceUnavailable && !text.Contains("sensitive-provider-body") &&
                !text.Contains("isolated-api-test-key"), purpose + " provider error JSON with HTTP 200 still returns sanitized API 503 when no usable code exists");
            check(handler.Requests == before + 1 && host.Fixture.Store.Accounts.Count == accountCount &&
                host.Fixture.Store.Challenges.Values.All(c => c.Consumed && c.CodeHash == null) && host.Fixture.Store.Grants.Count == 0,
                purpose + " provider errors cannot activate a challenge, issue a recovery grant, change accounts or retry SMS");
        }
    }
}
