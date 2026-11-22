using ChapGiti.Domain.Authentication;

namespace ChapGiti.Application.Features;

public sealed class AdministratorService(IAccountRepository accounts, IUnitOfWork unitOfWork,
    SessionService sessions, IPasswordHasher passwords)
{
    public async Task<UseCaseResult> List()
    {
        await sessions.Admin();
        return UseCaseResult.Json(await accounts.ListAdministrators());
    }

    public async Task<UseCaseResult> Create(JsonObject body)
    {
        await sessions.Admin();
        throw new UseCaseException(ErrorKind.Conflict, "نام کاربری مدیریت ثابت است؛ ایجاد حساب مدیر اضافی فعال نیست.");
    }

    public async Task<UseCaseResult> ChangeEmail(JsonObject body)
    {
        await sessions.Admin();
        await sessions.Limit("administrator-email", 10);
        var email = Email(body);
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            var actor = (await sessions.Admin())!;
            await Available(email, actor.Str("id"));
            if (await accounts.UpdateAdministratorEmail(actor.Str("id"), email) != 1)
                throw new UseCaseException(ErrorKind.Unauthenticated, "نشست مدیریت معتبر نیست.");
            await sessions.Audit(actor.Str("id"), "تغییر ایمیل پروفایل توسط خود مدیر");
            return true;
        });
        return UseCaseResult.Json(new { ok = true, email, message = "ایمیل شما تغییر کرد." });
    }

    public async Task<UseCaseResult> ChangePassword(JsonObject body)
    {
        await sessions.Admin();
        await sessions.Limit("administrator-password", 10);
        var current = Read(body, "currentPassword");
        if (current.Length is < 1 or > 128)
            throw new UseCaseException(ErrorKind.Invalid, "رمز فعلی را وارد کنید.");
        var hash = passwords.Hash(PasswordPolicy.Confirm(Read(body, "password"), Read(body, "confirmation")));
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            var actor = (await sessions.Admin())!;
            var account = await accounts.FindAdminById(actor.Str("id"));
            if (!passwords.Verify(current, account?.Str("password_hash")))
                throw new UseCaseException(ErrorKind.Invalid, "رمز عبور فعلی صحیح نیست.");
            if (await accounts.UpdateAdministratorPassword(actor.Str("id"), hash) != 1)
                throw new UseCaseException(ErrorKind.Unauthenticated, "نشست مدیریت معتبر نیست.");
            await sessions.RevokeLegacyAdmin(actor.Str("id"));
            await accounts.RevokeAdminSessions(actor.Str("id"));
            await sessions.Audit(actor.Str("id"), "تغییر رمز توسط خود مدیر و ابطال نشست‌های مدیریت");
            return true;
        });
        await sessions.Logout(true);
        return UseCaseResult.Json(new { ok = true, message = "رمز عبور تغییر کرد. دوباره وارد مدیریت شوید." });
    }

    private async Task Available(string email, string? ownId = null)
    {
        var owner = await accounts.FindAdmin(email);
        var historical = await accounts.FindAdminById(email);
        if ((owner != null && owner.Str("id") != ownId) || (historical != null && historical.Str("id") != ownId))
            throw new UseCaseException(ErrorKind.Conflict, "این ایمیل قبلاً استفاده شده است.");
    }

    private static string Email(JsonObject body)
    {
        var email = Input.Text(body, "email", 1, 254).ToLowerInvariant();
        if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            throw new UseCaseException(ErrorKind.Invalid, "ایمیل معتبر نیست.");
        return email;
    }

    private static string Read(JsonObject body, string key) =>
        body[key] is JsonValue v && v.TryGetValue<string>(out var value) ? value : "";
}
