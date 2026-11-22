using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Features;
using ChapGiti.Application.Common;
using ChapGiti.Infrastructure;
using ChapGiti.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChapGiti.AuthenticationTests;

internal static class OtpSwitchConfigurationScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        IConfiguration Config(string? setting = null, string site = "http://127.0.0.1:3000", string? nested = null)
        {
            var values = new Dictionary<string, string?> { ["Site:Url"] = site };
            if (setting != null) values["OTP_VERIFICATION_ENABLED"] = setting;
            if (nested != null) values["Otp:VerificationEnabled"] = nested;
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }
        var development = new TestEnvironment { EnvironmentName = "Development" };
        check(OtpVerificationConfiguration.Read(Config(), development).Enabled, "missing switch defaults to real verification in local Development");
        foreach (var environment in new[] { "Production", "Staging" })
        {
            var host = new TestEnvironment { EnvironmentName = environment };
            check(OtpVerificationConfiguration.Read(Config(), host).Enabled, "missing switch defaults to enabled in " + environment);
            try { OtpVerificationConfiguration.Read(Config("false"), host); throw new Exception("Unsafe configuration accepted."); }
            catch (InvalidOperationException) { check(true, "disabled verification is rejected in " + environment); }
        }
        check(OtpVerificationConfiguration.Read(Config("true", nested: "false"), development).Enabled &&
            !OtpVerificationConfiguration.Read(Config("false", nested: "true"), development).Enabled,
            "exact OTP_VERIFICATION_ENABLED setting overrides the nested default");
        foreach (var setting in new[] { "", "0", "no", "invalid" })
        {
            try { OtpVerificationConfiguration.Read(Config(setting), development); throw new Exception("Invalid boolean accepted."); }
            catch (InvalidOperationException) { check(true, "invalid OTP switch values fail closed"); }
        }
        foreach (var site in new[] { "https://printing.example", "http://203.0.113.10:8081", "ftp://localhost", "not-a-url" })
        {
            try { OtpVerificationConfiguration.Read(Config("false", site), development); throw new Exception("Public bypass accepted."); }
            catch (InvalidOperationException) { check(true, "Development bypass requires a local HTTP(S) site origin"); }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Site:Url"] = "http://localhost:3000", ["OTP_VERIFICATION_ENABLED"] = "false",
            ["Otp:Enabled"] = "true", ["Otp:CodeLength"] = "4", ["Otp:ApiKey"] = "configured-but-never-used-test-key",
            ["Sms:Enabled"] = "true", ["Sms:Username"] = "fake-test-user", ["Sms:Password"] = "fake-test-password",
            ["Sms:From"] = "fake-test-sender"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IHostEnvironment>(development);
        services.AddInfrastructure();
        services.AddScoped<ContentService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<IOtpSender>();
        check(!sender.Available, "real OTP sender is disabled even with enabled provider settings and credentials");
        var notifications = scope.ServiceProvider.GetRequiredService<ISmsNotifier>();
        check(!notifications.Availability.Enabled, "real order notification SMS is also disabled by the local switch");
        // No SQL connection string exists: this must return before both SQL logging and network I/O.
        await notifications.Notify("unused-order", "unused-code", AuthFixture.Phone, "new", "unused-event");
        check(true, "disabled order notifications do not write logs or attempt SQL/network access");

        var handler = new ProviderHandler();
        using var http = new HttpClient(handler);
        var realAdapter = new MelipayamakOtpSender(http, new MelipayamakOtpOptions(config), new(false));
        try { await realAdapter.Send(AuthFixture.Phone); throw new Exception("SMS was not blocked."); }
        catch (UseCaseException error) { check(error.Kind == ErrorKind.Unavailable && handler.Requests == 0, "disabled OTP adapter makes zero HTTP requests"); }
    }
}
