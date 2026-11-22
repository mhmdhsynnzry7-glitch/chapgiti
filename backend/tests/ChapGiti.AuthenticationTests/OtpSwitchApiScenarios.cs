using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ChapGiti.AuthenticationTests;

internal static class OtpSwitchApiScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        await using var host = await TestApiHost.Start(otpVerificationEnabled: false);
        host.Fixture.Sender.Available = false;
        host.Fixture.Sender.Fail = true;
        using var client = new HttpClient { BaseAddress = new Uri(host.Origin) };
        async Task<JsonObject> Post(string path, object body, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customer/" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
            if (response.StatusCode != expected) throw new InvalidOperationException(path + " returned " + response.StatusCode + ": " + data);
            return data;
        }
        const string phone = "09177777777";
        var challenge = await Post("otp/request", new { phone, purpose = "register" });
        check(challenge["codeLength"]!.GetValue<int>() == 4 &&
            new[] { "ok", "challengeId", "expiresAt", "resendAfterSeconds", "codeLength", "message" }.All(challenge.ContainsKey),
            "disabled-mode HTTP challenge preserves the existing UI response contract");
        await Post("otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "123" }, HttpStatusCode.BadRequest);
        var registration = await Post("otp/verify", new { challengeId = challenge["challengeId"]!.ToString(), code = "9876" });
        check(!host.Fixture.Store.Accounts.ContainsKey(phone), "bypass verification issues only a registration grant");
        await Post("registration/complete", AuthFixture.RegistrationBody(registration));
        check(host.Fixture.Store.Accounts.ContainsKey(phone) && host.Fixture.Sender.Sends == 0,
            "registration Controller accepts an arbitrary four-digit code with the SMS provider unavailable");
        await Post("login", new { phone, password = AuthFixture.Password });
        check(true, "the unchanged password-login step succeeds after development registration");

        var recovery = await Post("otp/request", new { phone = AuthFixture.Phone, purpose = "reset" });
        var grant = await Post("password/verify", new { challengeId = recovery["challengeId"]!.ToString(), code = "4567" });
        await Post("password/reset", new { resetToken = grant["resetToken"]!.ToString(), password = AuthFixture.NewPassword, confirmation = AuthFixture.NewPassword });
        check(host.Fixture.Passwords.Verify(AuthFixture.NewPassword, host.Fixture.Store.Accounts[AuthFixture.Phone].Hash) &&
            host.Fixture.Sender.Sends == 0, "recovery Controllers preserve the code/grant/new-password UI sequence without SMS");
    }
}
