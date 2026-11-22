using System.Security.Cryptography;
using System.Text;
using ChapGiti.Application.Authentication;
using ChapGiti.Domain.Authentication;

namespace ChapGiti.Application.Features;

public sealed class CustomerOtpService(
    IOtpRepository challenges,
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    SessionService sessions,
    IOtpSender sender,
    IOtpCodeProtector protector,
    IPasswordHasher passwords,
    TimeProvider clock,
    AuthenticationPolicy policy,
    OtpVerificationPolicy verification)
{
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();
    private static string Token() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task<UseCaseResult> Request(JsonObject body)
    {
        var phone = Input.Phone(body);
        var purpose = Input.Text(body, "purpose", 1, 16);
        if (purpose is not ("register" or "reset" or "verify-phone"))
            throw new UseCaseException(ErrorKind.Invalid, "نوع درخواست معتبر نیست.");
        await sessions.Limit("otp-request", 10);
        var bypass = verification.Bypasses(purpose);
        if (body.Any(field => field.Key is not ("phone" or "purpose")))
            throw new UseCaseException(ErrorKind.Invalid, "ابتدا فقط شماره موبایل را برای دریافت کد وارد کنید.");
        var currentCustomer = purpose == "verify-phone" ? await sessions.Customer() : null;
        if (currentCustomer != null && currentCustomer.Str("phone") != phone)
            throw new UseCaseException(ErrorKind.Forbidden, "فقط شمارهٔ حساب خود را می‌توانید تأیید کنید.");
        if (!verification.Enabled && !bypass)
            throw new UseCaseException(ErrorKind.Unavailable, "تأیید شمارهٔ حساب در حالت توسعهٔ بدون پیامک در دسترس نیست.");

        var now = Now;
        var challenge = await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            await challenges.LockPhone(phone);
            var account = await accounts.FindCustomerAccount(phone);
            if (purpose == "reset" && RecoveryFailure(account, phone) is { } recoveryError)
                throw recoveryError;
            if (purpose == "register" && account != null)
                throw new UseCaseException(ErrorKind.Conflict, "این شماره قبلاً ثبت‌نام کرده است. با رمز عبور وارد شوید.");
            if (!bypass && !sender.Available)
                throw CustomerOtpErrors.DeliveryFailed();
            var latest = await challenges.Latest(phone);
            if (latest != null && latest.CreatedAt + OtpPolicy.ResendSeconds * 1000L > now)
                throw new UseCaseException(ErrorKind.TooManyRequests, "برای ارسال دوبارهٔ کد کمی صبر کنید.",
                    (int)Math.Ceiling((latest.CreatedAt + OtpPolicy.ResendSeconds * 1000L - now) / 1000d));
            if (await challenges.CountRequests(phone, now - 900000) >= OtpPolicy.SendsPerQuarterHour)
                throw new UseCaseException(ErrorKind.TooManyRequests, "تعداد درخواست کد زیاد است. ۱۵ دقیقه دیگر تلاش کنید.", 900);
            if (await challenges.CountRequests(phone, now - 86400000) >= OtpPolicy.SendsPerDay)
                throw new UseCaseException(ErrorKind.TooManyRequests, "سقف ارسال کد امروز تکمیل شده است. بعداً تلاش کنید.");

            var eligible = purpose == "verify-phone" ? currentCustomer!.Str("id")
                : purpose == "reset" && account != null && Convert.ToBoolean(account.GetValueOrDefault("phone_verified"))
                    ? account.Str("registration_id") : null;
            var pending = new OtpChallenge(Token(), phone, purpose, eligible,
                null, now, now + policy.OtpLifetimeSeconds * 1000L, 0, false, "pending");
            await challenges.Invalidate(phone, now);
            await challenges.Insert(pending);
            return pending;
        });

        // Recovery eligibility is checked before any challenge or provider invocation.
        try
        {
            var hash = OtpVerificationPolicy.DevelopmentChallengeMarker;
            if (!bypass)
            {
                var code = await sender.Send(phone);
                if (!Regex.IsMatch(code, "\\A[0-9]{" + sender.CodeLength + "}\\z"))
                    throw CustomerOtpErrors.DeliveryFailed();
                hash = protector.Hash(challenge.Id, phone, purpose, code);
            }
            if (!await challenges.Activate(challenge.Id, hash, Now))
                throw CustomerOtpErrors.InvalidChallenge();
        }
        catch (UseCaseException error) when (error.Kind == ErrorKind.Unavailable)
        {
            await challenges.Fail(challenge.Id, Now);
            throw CustomerOtpErrors.DeliveryFailed();
        }
        catch
        {
            await challenges.Fail(challenge.Id, Now);
            throw;
        }

        return UseCaseResult.Json(new OtpChallengeResponse(true, challenge.Id, challenge.Expires,
            Math.Max(0, (int)Math.Ceiling((now + OtpPolicy.ResendSeconds * 1000L - Now) / 1000d)),
            bypass ? OtpVerificationPolicy.DevelopmentCodeLength : sender.CodeLength,
            bypass ? "در حالت توسعه یک کد ۴ رقمی وارد کنید؛ پیامکی ارسال نمی‌شود." : purpose == "reset"
                ? "کد بازیابی رمز عبور ارسال شد."
                : "کد تأیید ارسال شد."));
    }

    public Task<UseCaseResult> VerifyRegistration(JsonObject body) =>
        Verify(body, "register", async challenge =>
        {
            if (await accounts.LockAccountOwner(challenge.Phone) != null)
                throw ExistingAccount();
            var token = Token();
            var expires = Now + OtpPolicy.RegistrationLifetimeSeconds * 1000L;
            await challenges.AddRegistrationGrant(RegistrationDigest(token), challenge.Phone, expires, Now);
            // No customer/profile/password/session exists until the grant is consumed at completion.
            return new RegistrationGrantResponse(true, token, challenge.Phone, expires);
        });

    public async Task<UseCaseResult> CompleteRegistration(JsonObject body)
    {
        await sessions.Limit("registration-complete", 10);
        if (body.Any(field => field.Key is not ("registrationToken" or "firstName" or "lastName" or "password" or "confirmation")))
            throw new UseCaseException(ErrorKind.Invalid, "اطلاعات درخواست ثبت‌نام معتبر نیست.");
        var tokenHash = RegistrationDigest(ReadToken(body, "registrationToken"));
        var known = await challenges.FindRegistrationGrant(tokenHash);
        if (known == null || known.Consumed || known.Expires <= Now)
            throw InvalidRegistration();
        var first = Input.Text(body, "firstName", 2, 80);
        var last = Input.Text(body, "lastName", 2, 80);
        var password = ReadPassword(body, "password");
        if (password != ReadPassword(body, "confirmation"))
            throw new UseCaseException(ErrorKind.Invalid, "تکرار رمز عبور یکسان نیست.");
        var hash = passwords.Hash(password);
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            await challenges.LockPhone(known.Phone);
            var grant = await challenges.FindRegistrationGrant(tokenHash, true);
            if (grant == null || grant.Consumed || grant.Expires <= Now)
                throw InvalidRegistration();
            if (await accounts.LockAccountOwner(grant.Phone) != null)
                throw ExistingAccount();
            if (!await challenges.ConsumeRegistrationGrant(tokenHash, Now))
                throw InvalidRegistration();
            var id = Input.NewId();
            await accounts.CreateRegistration(id, grant.Phone, Input.Now());
            await accounts.UpdateProfile(first, last, Input.Now(), id);
            await accounts.VerifyPhone(id, Input.Now());
            await accounts.CreateAccount(grant.Phone, id, hash, Input.Now());
            await challenges.Invalidate(grant.Phone, Now);
            return true;
        });
        return UseCaseResult.Json(new { ok = true, message = "ثبت‌نام تکمیل شد. با شماره موبایل و رمز عبور وارد شوید." });
    }

    public async Task<UseCaseResult> VerifyPhone(JsonObject body)
    {
        var customer = (await sessions.Customer())!;
        return await Verify(body, "verify-phone", async challenge =>
        {
            if (challenge.CustomerId != customer.Str("id") || challenge.Phone != customer.Str("phone"))
                throw CustomerOtpErrors.InvalidChallenge();
            await accounts.VerifyPhone(customer.Str("id"), Input.Now());
            return new { ok = true, message = "شماره موبایل تأیید شد." };
        });
    }

    public Task<UseCaseResult> VerifyReset(JsonObject body) =>
        Verify(body, "reset", async challenge =>
        {
            var token = Token();
            var expires = Now + OtpPolicy.ResetLifetimeSeconds * 1000L;
            await challenges.RevokeResets(challenge.CustomerId!, Now);
            await challenges.AddReset(ResetDigest(token), challenge.CustomerId!, expires, Now);
            // A recovery grant never creates a login session and stays out of URLs and cookies.
            return new RecoveryGrantResponse(true, token, expires);
        });

    private async Task<UseCaseResult> Verify(
        JsonObject body, string purpose, Func<OtpChallenge, Task<object>> onSuccess)
    {
        await sessions.Limit("otp-verify", 30);
        var id = body["challengeId"] is JsonValue identifier && identifier.TryGetValue<string>(out var suppliedId)
            ? suppliedId.Trim() : "";
        if (!Regex.IsMatch(id, "^[a-f0-9]{64}$")) throw CustomerOtpErrors.Missing();
        var bypass = verification.Bypasses(purpose);
        // Bypass codes are exactly four digits: do not trim spaces or line breaks.
        var rawCode = body["code"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        var code = Digits(bypass ? rawCode : rawCode.Trim());
        var known = await challenges.Find(id);
        if (known == null)
            throw CustomerOtpErrors.Missing();

        var result = await unitOfWork.Transaction<object>(async () =>
        {
            await sessions.LockSecurity();
            // Consistent lock order across send, verify and recovery avoids cross-request races.
            await challenges.LockPhone(known.Phone);
            var current = await challenges.LockChallenge(id);
            var now = Now;
            if (current == null) return CustomerOtpErrors.Missing();
            if (current.Purpose != purpose || current.DeliveryStatus == "grant") return CustomerOtpErrors.InvalidChallenge();
            if (purpose == "reset")
            {
                var owner = await accounts.FindCustomerAccount(current.Phone);
                var eligibilityError = RecoveryFailure(owner, current.Phone);
                if (eligibilityError != null || current.CustomerId == null || owner!.Str("registration_id") != current.CustomerId)
                {
                    await challenges.Consume(id, now);
                    return eligibilityError ?? CustomerOtpErrors.InvalidChallenge();
                }
            }
            if (current.DeliveryStatus == "failed") return CustomerOtpErrors.DeliveryFailed();
            if (current.Attempts >= OtpPolicy.MaximumAttempts) return CustomerOtpErrors.AttemptLimit();
            if (current.Consumed) return CustomerOtpErrors.Consumed();
            if (current.Expires <= now) return CustomerOtpErrors.Expired();
            if (current.DeliveryStatus != "sent" || current.CodeHash == null) return CustomerOtpErrors.InvalidChallenge();

            var developmentChallenge = current.CodeHash == OtpVerificationPolicy.DevelopmentChallengeMarker;
            if (developmentChallenge != bypass) return CustomerOtpErrors.InvalidChallenge();
            var valid = bypass
                ? developmentChallenge && code.Length == OtpVerificationPolicy.DevelopmentCodeLength && Regex.IsMatch(code, "^[0-9]{4}$")
                : verification.Enabled && !developmentChallenge && Regex.IsMatch(code, "^[0-9]{4,10}$") &&
                    protector.Matches(current.CodeHash, current.Id, current.Phone, current.Purpose, code);
            if (!valid)
            {
                await challenges.RecordFailure(id, current.Attempts + 1 >= OtpPolicy.MaximumAttempts, now);
                // Return (rather than throw) so the failed-attempt counter commits.
                return current.Attempts + 1 >= OtpPolicy.MaximumAttempts
                    ? CustomerOtpErrors.AttemptLimit() : CustomerOtpErrors.Incorrect();
            }
            await challenges.Consume(id, now);
            return await onSuccess(current);
        });
        // Throw after the transaction so attempt counters and stale-challenge invalidation commit.
        if (result is UseCaseException failure) throw failure;
        return UseCaseResult.Json(result);
    }

    public async Task<UseCaseResult> ResetPassword(JsonObject body)
    {
        await sessions.Limit("password-reset", 10);
        var tokenHash = ResetDigest(ReadToken(body, "resetToken"));
        var password = ReadPassword(body, "password");
        var confirmation = ReadPassword(body, "confirmation");
        if (password != confirmation)
            throw new UseCaseException(ErrorKind.Invalid, "تکرار رمز عبور با رمز جدید یکسان نیست.");
        var known = await challenges.FindReset(tokenHash);
        if (known == null || known.Consumed || known.Expires <= Now)
            throw InvalidReset();

        var hash = passwords.Hash(password);
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            await challenges.LockPhone(known.Phone);
            var grant = await challenges.FindReset(tokenHash, true);
            if (grant == null || grant.Consumed || grant.Expires <= Now)
                throw InvalidReset();
            var owner = await accounts.LockAccountOwner(grant.Phone);
            if (owner?.Str("registration_id") != grant.CustomerId)
                throw InvalidReset();
            if (await accounts.UpdateCustomerPassword(grant.CustomerId, hash) != 1)
                throw InvalidReset();
            await accounts.RevokeCustomerSessions(grant.CustomerId);
            await sessions.RevokeCustomer(grant.CustomerId);
            await challenges.Invalidate(grant.Phone, Now);
            await challenges.RevokeResets(grant.CustomerId, Now);
            return true;
        });
        await sessions.Logout(false);
        return UseCaseResult.Json(new { ok = true, message = "رمز عبور تغییر کرد. دوباره وارد حساب شوید." });
    }

    private string ResetDigest(string token) => Digest(verification.Enabled ? token : "ChapGiti.DevelopmentReset.v1|" + token);

    private string RegistrationDigest(string token) =>
        Digest("ChapGiti.Registration.v1|" + (verification.Enabled ? "verified|" : "development|") + token);

    private static string ReadToken(JsonObject body, string key)
    {
        var value = Input.Text(body, key, 64, 64);
        if (!Regex.IsMatch(value, "^[a-f0-9]{64}$"))
            throw new UseCaseException(ErrorKind.Invalid, "درخواست معتبر نیست.");
        return value;
    }

    private static string ReadPassword(JsonObject body, string key)
    {
        if (body[key] is not JsonValue value || !value.TryGetValue<string>(out var password) ||
            !PasswordPolicy.IsValid(password))
            throw new UseCaseException(ErrorKind.Invalid, PasswordPolicy.Guidance);
        return password;
    }

    private static string Digits(string value)
    {
        const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩";
        return new string(value.Select(c => fa.Contains(c) ? (char)('0' + fa.IndexOf(c)) :
            ar.Contains(c) ? (char)('0' + ar.IndexOf(c)) : c).ToArray());
    }

    private static UseCaseException? RecoveryFailure(Dictionary<string, object?>? account, string phone)
    {
        if (account == null) return CustomerOtpErrors.AccountNotFound();
        if (string.IsNullOrWhiteSpace(account.Str("password_hash")) || account.Str("phone") != phone ||
            string.IsNullOrWhiteSpace(account.Str("first_name")) || string.IsNullOrWhiteSpace(account.Str("last_name")))
            return CustomerOtpErrors.IncompleteAccount();
        return Convert.ToBoolean(account.GetValueOrDefault("phone_verified")) ? null : CustomerOtpErrors.UnverifiedPhone();
    }

    private static UseCaseException InvalidReset() =>
        new(ErrorKind.Invalid, "مهلت تغییر رمز تمام شده یا درخواست استفاده شده است. دوباره کد بازیابی بگیرید.");

    private static UseCaseException InvalidRegistration() =>
        new(ErrorKind.Invalid, "مهلت تکمیل ثبت‌نام تمام شده یا درخواست معتبر نیست. دوباره شماره موبایل را تأیید کنید.",
            code: "REGISTRATION_GRANT_INVALID");

    private static UseCaseException ExistingAccount() =>
        new(ErrorKind.Conflict, "این شماره قبلاً ثبت‌نام کرده است. با رمز عبور وارد شوید.");
}
