using System.Text.Json.Nodes;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class RealOtpScenarios
{
    public static async Task Run(Action<bool, string> check)
    {
        foreach (var purpose in new[] { "register", "reset" })
        {
            foreach (var code in new[] { "4068", "۴۰۶۸", "٤٠٦٨", "۴0٦8" })
            {
                var fixture = new AuthFixture();
                if (purpose == "reset") fixture.Existing(true);
                fixture.Sender.Code = "4068";
                var challenge = await fixture.Send(purpose, "٠٩٠٠٠٠٠٠٨٢٦");
                var result = AuthFixture.Data(await fixture.Verify(challenge, code, reset: purpose == "reset"));
                check(result["ok"]!.GetValue<bool>() && fixture.Sender.Sends == 1 &&
                    fixture.Request.CustomerToken == null, purpose + " verifies the provider code with normalized English/Persian/Arabic digits and no passwordless session");
                await AuthScenarios.Reject(() => fixture.Verify(challenge, code, reset: purpose == "reset"), ErrorKind.Invalid);
                check(true, purpose + " rejects reuse of a consumed real-mode code");
            }

            {
                var fixture = new AuthFixture();
                if (purpose == "reset") fixture.Existing(true);
                var challenge = await fixture.Send(purpose);
                await AuthScenarios.Reject(() => fixture.Verify(challenge, "9876", reset: purpose == "reset"), ErrorKind.Invalid);
                var stored = fixture.Store.Challenges[challenge["challengeId"]!.ToString()];
                check(stored.Attempts == 1 && !stored.Consumed && stored.CodeHash != OtpVerificationPolicy.DevelopmentChallengeMarker,
                    purpose + " rejects arbitrary four-digit codes and retains a protected real-mode challenge");
                fixture.Clock.Advance(120);
                await AuthScenarios.Reject(() => fixture.Verify(challenge, reset: purpose == "reset"), ErrorKind.Invalid);
                check(true, purpose + " rejects the correct code at the exact 120-second expiry");
            }
            {
                var fixture = new AuthFixture();
                if (purpose == "reset") fixture.Existing(true);
                var challenge = await fixture.Send(purpose);
                for (var attempt = 0; attempt < 5; attempt++)
                    await AuthScenarios.Reject(() => fixture.Verify(challenge, "9876", reset: purpose == "reset"), ErrorKind.Invalid);
                await AuthScenarios.Reject(() => fixture.Verify(challenge, reset: purpose == "reset"), ErrorKind.Invalid);
                var stored = fixture.Store.Challenges[challenge["challengeId"]!.ToString()];
                check(stored.Attempts == 5 && stored.Consumed && stored.CodeHash == null,
                    purpose + " exhausts and clears the challenge after five wrong codes");
            }
            {
                var fixture = new AuthFixture();
                if (purpose == "reset") fixture.Existing(true);
                await fixture.Send(purpose);
                fixture.Clock.Advance(59);
                await AuthScenarios.Reject(async () => { await fixture.Send(purpose); return new(); }, ErrorKind.TooManyRequests);
                check(fixture.Sender.Sends == 1, purpose + " cooldown blocks sending at 59 seconds");
                fixture.Clock.Advance(1);
                await fixture.Send(purpose);
                check(fixture.Sender.Sends == 2, purpose + " resend is allowed at the exact 60-second boundary");
            }
        }

        var daily = new AuthFixture();
        var now = daily.Clock.GetUtcNow().ToUnixTimeMilliseconds();
        for (var n = 0; n < 20; n++)
        {
            var id = n.ToString("x64");
            daily.Store.Challenges.Add(id, new(id, AuthFixture.Phone, "reset", null, null,
                now - 3600000 - n, now - 3500000, 0, true, "sent"));
        }
        await AuthScenarios.Reject(async () => { await daily.Send(); return new(); }, ErrorKind.TooManyRequests);
        check(daily.Sender.Sends == 0, "the daily phone quota rejects a 21st send outside the quarter-hour window");
    }
}
