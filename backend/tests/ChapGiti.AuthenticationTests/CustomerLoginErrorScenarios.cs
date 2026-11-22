using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class CustomerLoginErrorScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        foreach (var phone in new[] { "09000000826", " ۰۹۰۰۰۰۰۰۸۲۶ ", "٠٩٠٠٠٠٠٠٨٢٦" })
        {
            var fixture = new AuthFixture();
            await Rejected(fixture, phone, CustomerLoginErrors.AccountNotFound,
                CustomerLoginErrors.AccountNotFoundMessage, check);
            check(fixture.Store.Accounts.Count == 0 && fixture.Store.Profiles.Count == 0,
                "unknown-mobile login cannot create an account or profile");

            fixture.Existing(true);
            var originalHash = fixture.Store.Accounts[AuthFixture.Phone].Hash;
            await Rejected(fixture, phone, CustomerLoginErrors.IncorrectPassword,
                CustomerLoginErrors.IncorrectPasswordMessage, check);
            check(fixture.Store.Accounts[AuthFixture.Phone].Hash == originalHash,
                "wrong-password login preserves the existing credential");
            await fixture.Accounts.LoginCustomer(new() { ["phone"] = phone, ["password"] = AuthFixture.Password });
            check(fixture.Request.CustomerToken != null && fixture.Request.RefreshToken != null,
                "password login still succeeds with normalized English, Persian or Arabic mobile digits");
        }

        {
            var fixture = new AuthFixture();
            for (var attempt = 0; attempt < 15; attempt++)
                await AuthScenarios.Reject(() => fixture.Accounts.LoginCustomer(new()
                    { ["phone"] = AuthFixture.Phone, ["password"] = "Wrong.Password@1" }), ErrorKind.Unauthenticated);
            await AuthScenarios.Reject(() => fixture.Accounts.LoginCustomer(new()
                { ["phone"] = AuthFixture.Phone, ["password"] = "Wrong.Password@1" }), ErrorKind.TooManyRequests);
            check(true, "distinct customer login errors retain the existing request rate limit");
        }

        await using var host = await TestApiHost.Start();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(host.Origin) };
        async Task<(HttpStatusCode Status, JsonObject Body)> Login(string path, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Origin", host.Origin);
            using var response = await client.SendAsync(request);
            check(!response.Headers.Contains("Set-Cookie"), "rejected login does not issue or change authentication cookies");
            return (response.StatusCode, (await response.Content.ReadFromJsonAsync<JsonObject>())!);
        }
        foreach (var phone in new[] { "09177777777", "۰۹۱۷۷۷۷۷۷۷۷", "٠٩١٧٧٧٧٧٧٧٧" })
        {
            var missing = await Login("customer/login", new { phone, password = "Wrong.Password@1" });
            check(missing.Status == HttpStatusCode.Unauthorized && missing.Body["code"]?.ToString() == CustomerLoginErrors.AccountNotFound &&
                missing.Body["error"]?.ToString() == CustomerLoginErrors.AccountNotFoundMessage,
                "unknown normalized mobile returns HTTP 401 and a structured account-not-found code");
        }
        foreach (var phone in new[] { AuthFixture.Phone, "۰۹۰۰۰۰۰۰۸۲۶", "٠٩٠٠٠٠٠٠٨٢٦" })
        {
            var incorrect = await Login("customer/login", new { phone, password = "Wrong.Password@1" });
            check(incorrect.Status == HttpStatusCode.Unauthorized && incorrect.Body["code"]?.ToString() == CustomerLoginErrors.IncorrectPassword &&
                incorrect.Body["error"]?.ToString() == CustomerLoginErrors.IncorrectPasswordMessage,
                "existing normalized mobile with a wrong password returns HTTP 401 and a distinct structured code");
            check(incorrect.Body.Count == 3, "customer login errors return no account or profile details");
        }
        var invalid = await Login("customer/login", new { phone = "invalid", password = "Wrong.Password@1" });
        check(invalid.Status == HttpStatusCode.BadRequest && !invalid.Body.ContainsKey("code"),
            "malformed mobile numbers remain validation errors instead of account-existence errors");
        var administrator = await Login("auth/login", new { username = "admin", password = "Wrong.Password@1" });
        check(administrator.Status == HttpStatusCode.Unauthorized && !administrator.Body.ContainsKey("code") &&
            administrator.Body["error"]?.ToString() == "نام کاربری یا رمز عبور صحیح نیست.",
            "administrator authentication retains its original generic error and response shape");
        check(host.Fixture.Store.Accounts.Count == 1 && host.Fixture.Store.Access.Count == 0 && host.Fixture.Store.Refresh.Count == 0 &&
            host.Fixture.Sender.Sends == 0 && host.Fixture.Store.Challenges.Count == 0,
            "failed HTTP logins create no accounts, sessions, refresh credentials or OTP sends");
    }

    private static async Task Rejected(AuthFixture fixture, string phone, string code, string message, Action<bool, string> check)
    {
        try
        {
            await fixture.Accounts.LoginCustomer(new() { ["phone"] = phone, ["password"] = "Wrong.Password@1" });
            throw new InvalidOperationException("Expected customer login rejection.");
        }
        catch (UseCaseException error)
        {
            check(error.Kind == ErrorKind.Unauthenticated && error.Code == code && error.Message == message,
                "customer use case distinguishes normalized missing accounts from incorrect passwords by code");
        }
        check(fixture.Request.CustomerToken == null && fixture.Request.RefreshToken == null && fixture.Store.Access.Count == 0 &&
            fixture.Store.Refresh.Count == 0 && fixture.Store.Challenges.Count == 0 && fixture.Sender.Sends == 0,
            "rejected customer login cannot create a session or trigger OTP");
    }
}
