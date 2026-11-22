using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChapGiti.Application.Common;
using ChapGiti.Application.Authentication;

namespace ChapGiti.Infrastructure.Messaging;

public sealed class MelipayamakOtpSender(HttpClient client, MelipayamakOtpOptions options, OtpVerificationPolicy verification) : IOtpSender
{
    public bool Available => verification.Enabled && options.Available;
    public int CodeLength => options.CodeLength;

    public async Task<string> Send(string phone)
    {
        if (!Available)
            throw Unavailable();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            // Console OTP contract: https://console.melipayamak.com/send/otp
            // POST {to}; the returned string code is used for verification.
            // This endpoint includes a secret. HTTP-client logging and redirects are disabled in DI.
            var endpoint = "https://console.melipayamak.com/api/send/otp/" + options.ApiKey;
            using var response = await client.PostAsJsonAsync(endpoint, new { to = phone }, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw Unavailable();
            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            if (json.Length > 4096)
                throw Unavailable();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("code", out var value) ||
                value.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("status", out var status) ||
                status.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
                throw Unavailable();
            // status is diagnostic text, not a documented boolean or success enum.
            // Its being non-empty does not invalidate a usable provider-issued OTP.
            // HTTP success alone is insufficient: always require the exact code format.
            var code = value.GetString() ?? "";
            if (!Regex.IsMatch(code, "\\A[0-9]{" + CodeLength + "}\\z"))
                throw Unavailable();
            return code;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Do not propagate request URLs, API keys or response bodies into logs.
            throw Unavailable();
        }
    }

    private static UseCaseException Unavailable() =>
        new(ErrorKind.Unavailable, "ارسال کد در حال حاضر امکان‌پذیر نیست. کمی بعد دوباره تلاش کنید یا با رمز عبور وارد شوید.");
}
