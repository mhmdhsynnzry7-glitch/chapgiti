using ChapGiti.Domain.Authentication;
using ChapGiti.Application.Authentication;

namespace ChapGiti.Application.Features;

public sealed class AdminUserService(IUserManagementRepository users, IAccountRepository accounts,
    IUnitOfWork unitOfWork, SessionService sessions, IPasswordHasher passwords)
{
    public async Task<UseCaseResult> List(string query, string role, string verified, int page, int pageSize, bool oldestFirst = false)
    {
        await sessions.Admin();
        if (query.Length > 100 || role is not ("" or "admin" or "customer") ||
            verified is not ("" or "true" or "false") || page < 1 || page > 100_000 || pageSize is < 1 or > 50)
            throw new UseCaseException(ErrorKind.Invalid, "فیلتر یا شمارهٔ صفحه معتبر نیست.");
        var result = await users.ListUsers(Input.NormalizeDigits(query.Trim()), role, verified, page, pageSize, oldestFirst);
        return UseCaseResult.Json(new UserPageResponse(result.Items.Select(PublicUser).ToArray(), result.Total, page, pageSize));
    }

    public async Task<UseCaseResult> Create(JsonObject body)
    {
        var actor = (await sessions.Admin())!;
        await sessions.Limit("admin-create-user", 30);
        if (body.Any(field => !new[] { "firstName", "lastName", "phone", "password", "confirmation" }.Contains(field.Key)))
            throw new UseCaseException(ErrorKind.Invalid, "فیلدهای ایجاد کاربر معتبر نیست.");
        var first = Input.Text(body, "firstName", 2, 80);
        var last = Input.Text(body, "lastName", 2, 80);
        var phone = Input.Phone(body);
        var password = body["password"]?.GetValue<string>() ?? "";
        var confirmation = body["confirmation"]?.GetValue<string>() ?? "";
        PasswordPolicy.Confirm(password, confirmation);
        var hash = passwords.Hash(password);
        var id = Input.NewId();
        await unitOfWork.Transaction(async () =>
        {
            await sessions.LockSecurity();
            await sessions.Admin(); // Recheck after serialization with concurrent role changes.
            if (await accounts.LockAccountOwner(phone) != null)
                throw new UseCaseException(ErrorKind.Conflict, "این شماره موبایل حساب دارد.");
            await accounts.CreateRegistration(id, phone, Input.Now());
            await accounts.UpdateProfile(first, last, Input.Now(), id);
            await accounts.CreateAccount(phone, id, hash, Input.Now());
            await sessions.Audit(actor.Str("email"), "ایجاد کاربر " + id);
            return true;
        });
        return UseCaseResult.Json(new { id, ok = true, phoneVerified = false }, created: true);
    }

    public async Task<UseCaseResult> SetAdministrator(string id, bool enabled)
    {
        await sessions.Admin();
        throw new UseCaseException(ErrorKind.Invalid, "مدیریت به حساب مستقل با ایمیل و رمز نیاز دارد؛ از بخش مدیران حساب بسازید.");
    }

    private static UserSummaryResponse PublicUser(Dictionary<string, object?> user) => new(
        user.Str("id"), user.Str("first_name"), user.Str("last_name"), user.Str("phone"), user.Str("created_at"),
        Convert.ToBoolean(user["phone_verified"]),
        Convert.ToBoolean(user["is_admin"]) ? ["customer", "admin"] : ["customer"],
        user.GetValueOrDefault("last_login_at") == null ? null : user.Num("last_login_at"),
        user.Num("login_count"), user.Num("active_sessions"), user.Num("order_count"));
}
