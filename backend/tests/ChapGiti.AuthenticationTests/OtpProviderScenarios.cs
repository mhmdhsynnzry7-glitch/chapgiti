using System.Net;
using System.Text.Json;
using ChapGiti.Application.Common;
using ChapGiti.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace ChapGiti.AuthenticationTests;

internal static class OtpProviderScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        // All provider output is simulated. Console generates/sends the OTP; these
        // tests validate its contract, not the randomness of an external service.
        const string key = "offline-provider-contract-test-key";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Otp:Enabled"] = "true", ["Otp:ApiKey"] = key, ["Otp:CodeLength"] = "4"
        }).Build();
        var handler = new ProviderHandler();
        using var client = new HttpClient(handler);
        var sender = new MelipayamakOtpSender(client, new(config), new(true));
        foreach (var code in new[] { "0000", "0123", "4068", "9999" })
        {
            handler.Response = JsonSerializer.Serialize(new { code, status = (string?)null });
            check(await sender.Send(AuthFixture.Phone) == code, "console-generated four-digit codes preserve their exact formatting and leading zeroes");
        }
        foreach (var status in new[] { "", " ", "\t\r\n" })
        {
            handler.Response = JsonSerializer.Serialize(new { code = "0123", status });
            check(await sender.Send(AuthFixture.Phone) == "0123", "empty legacy status values preserve successful OTP parsing");
        }
        // Synthetic strings exercise the documented free-text field. None is
        // asserted to be the exact, unretained status of the previous live send.
        foreach (var status in new[] { "provider diagnostic text", "شرح خطا در صورت بروز", "ارسال موفق بود" })
        {
            handler.Response = JsonSerializer.Serialize(new { code = "0123", status });
            var before = handler.Requests;
            check(await sender.Send(AuthFixture.Phone) == "0123" && handler.Requests == before + 1,
                "a valid provider code is accepted with non-empty string diagnostics without an invented status enum or retry");
        }
        async Task Rejected(string name)
        {
            var before = handler.Requests;
            try { await sender.Send(AuthFixture.Phone); throw new InvalidOperationException("Provider failure was accepted."); }
            catch (UseCaseException error)
            {
                check(error.Kind == ErrorKind.Unavailable && !error.ToString().Contains(key) &&
                    !error.ToString().Contains("sensitive-provider-body") && handler.Requests == before + 1,
                    name + " fails closed, hides secrets and does not retry SMS");
            }
        }
        foreach (var code in new[] { "123", "12345", "0123\n", "0123 ", " 0123", "۱۲۳۴", "١٢٣٤", "12x4", "" })
        {
            handler.Response = JsonSerializer.Serialize(new { code, status = "provider diagnostic text" });
            await Rejected("malformed provider OTP");
        }
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
            HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
        {
            handler.Status = status;
            // Even a usable-looking code must not override an HTTP failure.
            handler.Response = "{\"code\":\"0123\",\"status\":\"provider diagnostic text\"}";
            await Rejected("provider HTTP " + (int)status);
        }
        handler.Status = HttpStatusCode.OK;
        foreach (var response in new[] { "", "not JSON", "null", "[]", "{}", "{\"code\":1234,\"status\":null}",
            "{\"code\":\"0123\"}", "{\"status\":null}", "{\"code\":null,\"status\":null}",
            "{\"code\":false,\"status\":null}", "{\"code\":\"0123\",\"status\":false}",
            "{\"code\":\"0123\",\"status\":true}", "{\"code\":\"0123\",\"status\":1}",
            "{\"code\":\"0123\",\"status\":{}}", "{\"code\":\"0123\",\"status\":[]}",
            "{\"code\":null,\"status\":\"sensitive-provider-body\"}",
            "{\"code\":\"\",\"status\":\"rejected\"}", "{\"status\":\"success\"}",
            "{\"code\":\"\",\"status\":\"ارسال موفق بود\"}", new string('x', 4097),
            JsonSerializer.Serialize(new { code = "0123", status = new string('x', 4096) }) })
        {
            handler.Response = response;
            await Rejected("invalid/error provider response");
        }
        foreach (var failure in new Exception[] { new HttpRequestException(key + "/sensitive-provider-body"),
            new TaskCanceledException(key + "/sensitive-provider-body") })
        {
            handler.Failure = failure;
            await Rejected("provider network/timeout exception");
        }
        handler.Failure = null;
        config["Otp:Enabled"] = "false";
        var previous = handler.Requests;
        try { await sender.Send(AuthFixture.Phone); throw new InvalidOperationException("Disabled provider sent SMS."); }
        catch (UseCaseException error) { check(error.Kind == ErrorKind.Unavailable && previous == handler.Requests,
            "disabled provider fails before network access even with real verification enabled"); }
    }
}
