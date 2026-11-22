using System.Text.Json.Nodes;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace ChapGiti.AuthenticationTests;

internal static class OtpSwitchScenarios
{
    private static AuthFixture Disabled()
    {
        var fixture = new AuthFixture(verificationEnabled: false, providerConfigured: false);
        fixture.Sender.Available = false;
        fixture.Sender.Fail = true;
        fixture.Sender.CodeLength = 10;
        return fixture;
    }
    private static JsonObject Code(JsonObject challenge, string value) =>
        new() { ["challengeId"] = challenge["challengeId"]!.ToString(), ["code"] = value };

    public static async Task Run(Action<bool, string> check)
    {
        foreach (var code in new[] { "0000", "9999", "1357", "۰۱۲۳", "١٢٣٤" })
        {
            var f = Disabled();
            var challenge = await f.Send();
            check(challenge["codeLength"]!.GetValue<int>() == 4, "bypass advertises exactly four digits without requiring provider configuration");
            var registrationGrant = AuthFixture.Data(await f.Verify(challenge, code));
            check(f.Store.Accounts.Count == 0, "bypass OTP still requires separate profile completion");
            await f.Complete(registrationGrant);
            check(f.Store.Accounts.Count == 1 && f.Sender.Sends == 0 && f.Request.CustomerToken == null,
                "registration accepts " + code + " without SMS, a provider key or passwordless login");
            await AuthScenarios.Reject(() => f.Verify(challenge, code), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 1, "bypass verification remains single-use");
        }
        foreach (var code in new[] { "", "123", "12345", "12a4", "1.23", " 1234", "1234 ", "1234\n" })
        {
            var f = Disabled();
            var challenge = await f.Send();
            await AuthScenarios.Reject(() => f.Verify(challenge, code), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 0 && f.Store.Challenges[challenge["challengeId"]!.ToString()].Attempts == 1,
                "bypass rejects non-exact four-digit input and commits the attempt: " + System.Text.Json.JsonSerializer.Serialize(code));
        }
        {
            var f = Disabled(); var challenge = await f.Send();
            await AuthScenarios.Reject(() => f.Otp.VerifyRegistration(new() { ["challengeId"] = challenge["challengeId"]!.ToString(), ["code"] = 1234 }), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 0, "a JSON number is not accepted in place of a four-digit OTP string");
            f.Clock.Advance(120);
            await AuthScenarios.Reject(() => f.Verify(challenge, "1111"), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 0, "bypass retains the two-minute expiry");
        }
        {
            var f = Disabled(); var challenge = await f.Send();
            for (var attempt = 0; attempt < 5; attempt++)
                await AuthScenarios.Reject(() => f.Verify(challenge, "123"), ErrorKind.Invalid);
            await AuthScenarios.Reject(() => f.Verify(challenge, "1111"), ErrorKind.Invalid);
            check(f.Store.Challenges[challenge["challengeId"]!.ToString()].Attempts == 5, "bypass retains the verification attempt limit");
            await AuthScenarios.Reject(async () => { await f.Send(); return new(); }, ErrorKind.TooManyRequests);
            check(f.Sender.Sends == 0, "bypass retains resend cooldown without sending SMS");
            await AuthScenarios.Reject(() => f.Otp.VerifyRegistration(new() { ["challengeId"] = new string('f', 64), ["code"] = "1111" }), ErrorKind.Invalid);
            check(true, "bypass still requires a server-issued challenge");
        }
        {
            var f = Disabled(); f.Existing(true);
            var challenge = await f.Send("reset");
            await AuthScenarios.Reject(() => f.Verify(challenge, "1111"), ErrorKind.Invalid);
            var grant = AuthFixture.Data(await f.Verify(challenge, "2468", reset: true));
            await f.Otp.ResetPassword(AuthFixture.ResetBody(grant));
            check(f.Passwords.Verify(AuthFixture.NewPassword, f.Store.Accounts[AuthFixture.Phone].Hash) && f.Sender.Sends == 0,
                "password recovery accepts any four digits, then enforces the normal reset grant and password rules");
            await AuthScenarios.Reject(() => f.Otp.ResetPassword(AuthFixture.ResetBody(grant)), ErrorKind.Invalid);
            check(true, "bypass recovery grants remain single-use");
        }
        {
            var f = Disabled(); f.Existing(false);
            await AuthScenarios.Reject(async () => { await f.Send("reset"); return new(); }, ErrorKind.Forbidden);
            check(f.Store.Grants.Count == 0 && !f.Store.Profiles.Single().Value.Verified, "bypass does not alter legacy recovery eligibility or existing verification records");
            await f.Accounts.LoginCustomer(new() { ["phone"] = AuthFixture.Phone, ["password"] = AuthFixture.Password });
            await AuthScenarios.Reject(async () => { await f.Send("verify-phone"); return new(); }, ErrorKind.Unavailable);
            check(f.Sender.Sends == 0, "authenticated legacy-phone verification is not bypassed and cannot send real SMS while disabled");
        }
        {
            var f = Disabled(); var challenge = await f.Send();
            await AuthScenarios.Reject(() => f.OtpInMode(true).VerifyRegistration(Code(challenge, "0123")), ErrorKind.Invalid);
            check(f.Store.Accounts.Count == 0, "re-enabling verification rejects an outstanding bypass challenge");
            var normal = new AuthFixture(); challenge = await normal.Send();
            await AuthScenarios.Reject(() => normal.OtpInMode(false).VerifyRegistration(Code(challenge, "0123")), ErrorKind.Invalid);
            check(normal.Store.Accounts.Count == 0, "disabling verification cannot downgrade a previously issued normal challenge");
        }
        foreach (var enabled in new[] { false, true })
        {
            var f = new AuthFixture(verificationEnabled: enabled); f.Existing(true);
            var grant = AuthFixture.Data(await f.Verify(await f.Send("reset"), enabled ? "0123" : "7777", reset: true));
            await AuthScenarios.Reject(() => f.OtpInMode(!enabled).ResetPassword(AuthFixture.ResetBody(grant)), ErrorKind.Invalid);
            check(f.Passwords.Verify(AuthFixture.Password, f.Store.Accounts[AuthFixture.Phone].Hash) && f.Store.Grants.Values.All(g => !g.Consumed),
                "recovery grants issued in one mode cannot be consumed in the other mode");
        }
        var configured = new AuthFixture();
        var realChallenge = await configured.Send();
        await AuthScenarios.Reject(() => configured.Verify(realChallenge, "9999"), ErrorKind.Invalid);
        await configured.Complete(AuthFixture.Data(await configured.Verify(realChallenge, "0123")));
        check(configured.Sender.Sends == 1 && configured.Store.Accounts.Count == 1, "enabled mode sends a provider code and requires its actual value");
    }
}
